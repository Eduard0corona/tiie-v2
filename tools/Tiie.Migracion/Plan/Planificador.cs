using System.Globalization;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Solicitantes;
using Tiie.Migracion.Origen;
using Tiie.Migracion.Reportes;

namespace Tiie.Migracion.Plan;

/// <param name="Roles">Vacío en <c>analizar</c>: los usuarios se reportan sin rol.</param>
/// <param name="Decisiones">CURP → folio anterior que se conserva cuando hay varias citas futuras.</param>
public sealed record OpcionesPlan(DateOnly FechaCorte, MapaRoles? Roles, IReadOnlyDictionary<string, string> Decisiones);

public sealed record ModuloPlan(
    int IdAnterior,
    short MunicipioId,
    string Nombre,
    string Calle,
    string NumeroExterior,
    string? NumeroInterior,
    string Colonia,
    string CodigoPostal,
    string? Localidad,
    string? Telefono,
    string? Correo,
    bool Activo);

/// <param name="Clave">Nombre normalizado que agrupa los trámites de 2016.</param>
public sealed record TipoPlan(string Clave, string Nombre, string? Descripcion, decimal? Costo, string? Notas, string? Requisito);

/// <param name="IdAnterior">Trámite de 2016 que da origen a la oferta; si se fusionaron varios, el de menor ID.</param>
public sealed record OfertaPlan(int IdAnterior, int ModuloIdAnterior, string ClaveTipo, bool Activo, string? NotasSede, IReadOnlyList<int> Tramites);

public sealed record PlantillaPlan(int OfertaIdAnterior, DayOfWeek Dia, TimeOnly Hora, byte Turnos);

/// <param name="Curp">CURP normalizada; vacía si no tiene 18 caracteres y no se puede guardar.</param>
public sealed record CitaPlan(CitaOrigen Cita, string? Curp, bool CurpValida);

public sealed record CupoPlan(int OfertaIdAnterior, DateOnly Fecha, TimeOnly Hora, short Turnos, IReadOnlyList<CitaPlan> Citas)
{
    /// <summary>La mayor entre los turnos y las citas: ninguna cita migrada se cancela por falta de lugar.</summary>
    public short Capacidad => (short)Math.Max(Turnos, Citas.Count);

    public bool Sobrecupo => Citas.Count > Turnos;
}

public sealed record HistoricoPlan(DateOnly Fecha, int OfertaIdAnterior, int Total);

public sealed record UsuarioPlan(UsuarioOrigen Usuario, string Rol, int? ModuloIdAnterior);

/// <summary>Resultado de aplicar las reglas de docs/14#mapeo a los datos de 2016, sin tocar ninguna base.</summary>
public sealed class PlanMigracion
{
    public required IReadOnlyList<EntidadOrigen> Entidades { get; init; }
    public required IReadOnlyList<MunicipioOrigen> Municipios { get; init; }
    public required IReadOnlyList<ModuloPlan> Modulos { get; init; }
    public required IReadOnlyList<TipoPlan> Tipos { get; init; }
    public required IReadOnlyList<OfertaPlan> Ofertas { get; init; }
    public required IReadOnlyList<PlantillaPlan> Plantillas { get; init; }
    public required IReadOnlyList<CupoPlan> Cupos { get; init; }
    public required IReadOnlyList<HistoricoPlan> Historicos { get; init; }
    public required IReadOnlyList<UsuarioPlan> Usuarios { get; init; }

    /// <summary>Valores de <c>Usuario.Tipo</c> de usuarios activos que el mapa de roles no cubre.</summary>
    public required IReadOnlyList<int> TiposSinRol { get; init; }

    /// <summary>Citas vigentes futuras del origen, antes de excepciones y duplicados.</summary>
    public required int CitasFuturasOrigen { get; init; }

    public required IReadOnlyList<Reporte> Reportes { get; init; }

    public int CitasAMigrar => Cupos.Sum(c => c.Citas.Count);
}

public static class Planificador
{
    /// <summary>Trámites de 2016 que no abrían en sábado (regla del JavaScript anterior).</summary>
    public static readonly IReadOnlySet<int> TramitesSinSabado = new HashSet<int> { 21, 22, 23 };

    private static readonly TimeOnly InicioSabado = new(9, 0);
    private static readonly TimeOnly FinSabado = new(12, 30);
    private static readonly TimeOnly HoraMinimaNormal = new(7, 0);
    private static readonly TimeOnly HoraMaximaNormal = new(19, 0);

    /// <summary>Lunes a viernes; sábado solo de 09:00 a 12:30 y si el trámite no es 21, 22 ni 23; domingo nunca.</summary>
    public static IEnumerable<DayOfWeek> DiasDePlantilla(int tramiteIdAnterior, TimeOnly hora)
    {
        yield return DayOfWeek.Monday;
        yield return DayOfWeek.Tuesday;
        yield return DayOfWeek.Wednesday;
        yield return DayOfWeek.Thursday;
        yield return DayOfWeek.Friday;
        if (hora >= InicioSabado && hora <= FinSabado && !TramitesSinSabado.Contains(tramiteIdAnterior))
            yield return DayOfWeek.Saturday;
    }

    /// <summary>El panel anterior guardaba horas PM como AM: antes de las 07:00 o después de las 19:00 se revisa.</summary>
    public static bool EsHoraSospechosa(TimeOnly hora) => hora < HoraMinimaNormal || hora > HoraMaximaNormal;

    public static PlanMigracion Crear(DatosOrigen datos, OpcionesPlan opciones)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(opciones);
        var r = new ReportesPlan();

        var municipios = datos.Municipios.OrderBy(m => m.MunicipioId).ToList();
        var modulos = PlanearModulos(datos, municipios.Select(m => m.MunicipioId).ToHashSet(), r);
        var modulosPorId = modulos.ToDictionary(m => m.IdAnterior);

        var tramites = datos.Tramites.OrderBy(t => t.TramiteId).ToList();
        var grupos = tramites.GroupBy(t => Normalizacion.ClaveNombre(t.Nombre)).ToList();
        var tipos = grupos.Select(g => new TipoPlan(
            g.Key,
            Normalizacion.MasComun(g.Select(t => Normalizacion.Limpiar(t.Nombre, 120)))!,
            Normalizacion.MasComun(g.Select(t => Normalizacion.Limpiar(t.Detalle, 500))),
            Normalizacion.MasComunValor(g.Select(t => t.Costo)),
            Normalizacion.MasComun(g.Select(t => Normalizacion.Limpiar(t.Notas))),
            Normalizacion.MasComun(g.Select(t => Normalizacion.Limpiar(t.Requisitos))))).ToList();
        foreach (var (tipo, grupo) in tipos.Zip(grupos))
            r.Tipos.Agregar(tipo.Nombre, string.Join(' ', grupo.Select(t => t.TramiteId)), tipo.Requisito);

        var (ofertas, ofertaDeTramite) = PlanearOfertas(tramites, grupos, tipos, modulosPorId, r);
        var plantillas = PlanearPlantillas(datos, tramites, ofertaDeTramite, modulosPorId, r);
        var (cupos, citasFuturas) = PlanearCitas(datos, opciones, ofertaDeTramite, r);
        var historicos = PlanearHistoricos(datos, ofertaDeTramite, r);
        var (usuarios, tiposSinRol) = PlanearUsuarios(datos, opciones, modulosPorId, r);

        return new PlanMigracion
        {
            Entidades = [.. datos.Entidades.OrderBy(e => e.EntidadId)],
            Municipios = municipios,
            Modulos = modulos,
            Tipos = tipos,
            Ofertas = ofertas,
            Plantillas = plantillas,
            Cupos = cupos,
            Historicos = historicos,
            Usuarios = usuarios,
            TiposSinRol = tiposSinRol,
            CitasFuturasOrigen = citasFuturas,
            Reportes = r.Todos,
        };
    }

    private static List<ModuloPlan> PlanearModulos(DatosOrigen datos, HashSet<int> municipios, ReportesPlan r)
    {
        var modulos = new List<ModuloPlan>();
        foreach (var m in datos.Modulos.OrderBy(m => m.ModuloId))
        {
            if (!municipios.Contains(m.MunicipioId))
            {
                r.Modulos.Agregar(Texto(m.ModuloId), m.Nombre, $"El municipio {m.MunicipioId} no existe: no se migra");
                continue;
            }
            var faltantes = new List<string>();
            string Requerido(string? valor, string campo, int maximo)
            {
                var limpio = Normalizacion.Limpiar(valor, maximo);
                if (limpio is null)
                    faltantes.Add(campo);
                return limpio ?? "Sin dato";
            }
            var codigoPostal = Normalizacion.Digitos(m.CodigoPostal, 5);
            if (codigoPostal is null)
                faltantes.Add("código postal");
            var telefono = Normalizacion.Digitos(m.Telefono, 10);
            if (m.Telefono is not null && telefono is null)
                faltantes.Add("teléfono de 10 dígitos");

            modulos.Add(new ModuloPlan(
                m.ModuloId,
                (short)m.MunicipioId,
                Normalizacion.Limpiar(m.Nombre, 120)!,
                Requerido(m.Calle, "calle", 100),
                Normalizacion.Limpiar(m.NumExt, 15) ?? Modulo.SinNumero,
                Normalizacion.Limpiar(m.NumInt, 15),
                Requerido(m.Colonia, "colonia", 80),
                codigoPostal ?? "00000",
                Normalizacion.Limpiar(m.Poblacion, 80),
                telefono,
                Normalizacion.Limpiar(m.Email, 150),
                m.Activo));
            if (faltantes.Count > 0)
                r.Modulos.Agregar(Texto(m.ModuloId), m.Nombre, "Completar en el panel: " + string.Join(", ", faltantes));
        }
        return modulos;
    }

    private static (List<OfertaPlan>, Dictionary<int, int>) PlanearOfertas(
        List<TramiteOrigen> tramites,
        List<IGrouping<string, TramiteOrigen>> grupos,
        List<TipoPlan> tipos,
        Dictionary<int, ModuloPlan> modulos,
        ReportesPlan r)
    {
        var clavePorTramite = grupos.SelectMany(g => g.Select(t => (t.TramiteId, g.Key))).ToDictionary(x => x.TramiteId, x => x.Key);
        var tipoPorClave = tipos.ToDictionary(t => t.Clave);
        var porModuloYTipo = new Dictionary<(int Modulo, string Tipo), List<TramiteOrigen>>();
        var ofertaDeTramite = new Dictionary<int, int>();

        foreach (var t in tramites)
        {
            if (t.ModuloId is not { } moduloId || !modulos.ContainsKey(moduloId))
            {
                r.TramitesSinModulo.Agregar(Texto(t.TramiteId), t.Nombre, t.ModuloId is { } m ? Texto(m) : null);
                continue;
            }
            var clave = (moduloId, clavePorTramite[t.TramiteId]);
            if (porModuloYTipo.TryGetValue(clave, out var existentes))
            {
                // Una sede no puede tener dos ofertas del mismo tipo (UX_Oferta_ModuloTipo): se fusionan.
                r.Fusionadas.Agregar(Texto(moduloId), tipoPorClave[clave.Item2].Nombre, Texto(existentes[0].TramiteId), Texto(t.TramiteId));
                existentes.Add(t);
                ofertaDeTramite[t.TramiteId] = existentes[0].TramiteId;
            }
            else
            {
                porModuloYTipo[clave] = [t];
                ofertaDeTramite[t.TramiteId] = t.TramiteId;
            }
        }

        var ofertas = porModuloYTipo.Select(o =>
        {
            var tipo = tipoPorClave[o.Key.Tipo];
            var principal = o.Value[0];
            var notas = new List<string>();
            if (Normalizacion.Limpiar(principal.Notas) is { } n && n != tipo.Notas)
            {
                notas.Add("Notas de la sede: " + n);
                r.Diferencias.Agregar(Texto(principal.TramiteId), tipo.Nombre, "Notas", n, tipo.Notas);
            }
            if (Normalizacion.Limpiar(principal.Requisitos) is { } q && q != tipo.Requisito)
            {
                notas.Add("Requisitos de la sede: " + q);
                r.Diferencias.Agregar(Texto(principal.TramiteId), tipo.Nombre, "Requisitos", q, tipo.Requisito);
            }
            return new OfertaPlan(
                principal.TramiteId,
                o.Key.Modulo,
                o.Key.Tipo,
                o.Value.Any(t => t.Activo) && modulos[o.Key.Modulo].Activo,
                notas.Count == 0 ? null : string.Join("\n", notas),
                [.. o.Value.Select(t => t.TramiteId)]);
        }).ToList();
        return (ofertas, ofertaDeTramite);
    }

    private static List<PlantillaPlan> PlanearPlantillas(
        DatosOrigen datos, List<TramiteOrigen> tramites, Dictionary<int, int> ofertaDeTramite, Dictionary<int, ModuloPlan> modulos, ReportesPlan r)
    {
        var tramitePorId = tramites.ToDictionary(t => t.TramiteId);
        var turnos = new Dictionary<(int Oferta, DayOfWeek Dia, TimeOnly Hora), byte>();
        foreach (var h in datos.Horarios.OrderBy(h => h.HorarioId))
        {
            if (!ofertaDeTramite.TryGetValue(h.TramiteId, out var oferta))
                continue;
            if (h.Activo && EsHoraSospechosa(h.Hora))
            {
                var tramite = tramitePorId[h.TramiteId];
                r.HorasSospechosas.Agregar(Texto(h.TramiteId), tramite.Nombre, modulos[tramite.ModuloId!.Value].Nombre,
                    Texto(h.HorarioId), h.Hora.ToString("HH:mm", CultureInfo.InvariantCulture), Texto(h.Turnos));
            }
            if (!h.Activo || h.Turnos <= 0)
                continue;
            var t = (byte)Math.Min(h.Turnos, byte.MaxValue);
            foreach (var dia in DiasDePlantilla(h.TramiteId, h.Hora))
                turnos[(oferta, dia, h.Hora)] = Math.Max(turnos.GetValueOrDefault((oferta, dia, h.Hora)), t);
        }
        return [.. turnos.Select(t => new PlantillaPlan(t.Key.Oferta, t.Key.Dia, t.Key.Hora, t.Value))
            .OrderBy(p => p.OfertaIdAnterior).ThenBy(p => p.Dia).ThenBy(p => p.Hora)];
    }

    private static (List<CupoPlan>, int) PlanearCitas(
        DatosOrigen datos, OpcionesPlan opciones, Dictionary<int, int> ofertaDeTramite, ReportesPlan r)
    {
        var turnosPorHorario = datos.Horarios.ToDictionary(h => h.HorarioId, h => h.Turnos);
        var futuras = datos.CitasVigentes.Where(c => c.FechaCita >= opciones.FechaCorte).OrderBy(c => c.CitaId).ToList();
        var candidatas = new List<(CitaPlan Plan, int Oferta, short Turnos)>();

        foreach (var c in futuras)
        {
            if (c.HorarioId is not { } horarioId || c.TramiteId is not { } tramiteId || c.Hora is null)
            {
                r.Excepciones.Agregar(c.Folio, Fecha(c.FechaCita), "La cita no tiene horario o su horario ya no existe");
                continue;
            }
            if (!ofertaDeTramite.TryGetValue(tramiteId, out var oferta))
            {
                r.Excepciones.Agregar(c.Folio, Fecha(c.FechaCita), $"El trámite {tramiteId} no tiene un módulo válido");
                continue;
            }
            var curp = string.IsNullOrWhiteSpace(c.Curp) ? null : Curp.Normalizar(c.Curp);
            var valida = curp is not null && Curp.EsValida(curp);
            if (!valida)
                r.CurpsInvalidas.Agregar(c.Folio, c.Curp, curp is { Length: Curp.Longitud } ? "Se migra con esta CURP" : "Se migra sin CURP");
            if (curp is { Length: not Curp.Longitud })
                curp = null;
            var turnos = (short)Math.Clamp(turnosPorHorario.GetValueOrDefault(horarioId, 1), 1, short.MaxValue);
            candidatas.Add((new CitaPlan(c, curp, valida), oferta, turnos));
        }

        // RN-01: una cita Agendada por CURP. Sin decisión de operación, ninguna de las repetidas se migra.
        var excluidas = new HashSet<int>();
        foreach (var grupo in candidatas.Where(x => x.Plan.Curp is not null).GroupBy(x => x.Plan.Curp!).Where(g => g.Count() > 1))
        {
            var decision = opciones.Decisiones.GetValueOrDefault(grupo.Key);
            var conservada = decision is null
                ? null
                : grupo.Select(x => x.Plan.Cita).FirstOrDefault(c => string.Equals(c.Folio, decision, StringComparison.OrdinalIgnoreCase));
            foreach (var x in grupo)
            {
                var cita = x.Plan.Cita;
                var resultado = conservada is null
                    ? decision is null ? "Sin decisión: no se migra" : $"El folio decidido ({decision}) no está entre estas citas: no se migra"
                    : cita.CitaId == conservada.CitaId ? "Se conserva" : "Se descarta por decisión";
                if (conservada is null || cita.CitaId != conservada.CitaId)
                    excluidas.Add(cita.CitaId);
                r.Duplicados.Agregar(grupo.Key, cita.Folio, Fecha(cita.FechaCita), Hora(cita.Hora!.Value), Texto(x.Oferta), resultado);
            }
        }

        var cupos = candidatas
            .Where(x => !excluidas.Contains(x.Plan.Cita.CitaId))
            .GroupBy(x => (x.Oferta, x.Plan.Cita.FechaCita, Hora: x.Plan.Cita.Hora!.Value))
            .Select(g => new CupoPlan(g.Key.Oferta, g.Key.FechaCita, g.Key.Hora, g.Max(x => x.Turnos), [.. g.Select(x => x.Plan)]))
            .OrderBy(c => c.Fecha).ThenBy(c => c.OfertaIdAnterior).ThenBy(c => c.Hora)
            .ToList();
        foreach (var c in cupos.Where(c => c.Sobrecupo))
            r.Sobrecupos.Agregar(Texto(c.OfertaIdAnterior), Fecha(c.Fecha), Hora(c.Hora), Texto(c.Turnos), Texto(c.Citas.Count));
        return (cupos, futuras.Count);
    }

    private static List<HistoricoPlan> PlanearHistoricos(DatosOrigen datos, Dictionary<int, int> ofertaDeTramite, ReportesPlan r)
    {
        var totales = new Dictionary<(DateOnly Fecha, int Oferta), int>();
        var sinOferta = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in datos.Historicos)
        {
            if (t.TramiteId is { } tramiteId && ofertaDeTramite.TryGetValue(tramiteId, out var oferta))
                totales[(t.Fecha, oferta)] = totales.GetValueOrDefault((t.Fecha, oferta)) + t.Total;
            else
            {
                var mes = t.Fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                sinOferta[mes] = sinOferta.GetValueOrDefault(mes) + t.Total;
            }
        }
        foreach (var (mes, total) in sinOferta)
            r.HistoricoSinOferta.Agregar(mes, Texto(total));
        return [.. totales.Select(t => new HistoricoPlan(t.Key.Fecha, t.Key.Oferta, t.Value)).OrderBy(h => h.Fecha).ThenBy(h => h.OfertaIdAnterior)];
    }

    private static (List<UsuarioPlan>, List<int>) PlanearUsuarios(
        DatosOrigen datos, OpcionesPlan opciones, Dictionary<int, ModuloPlan> modulos, ReportesPlan r)
    {
        var usuarios = new List<UsuarioPlan>();
        var tiposSinRol = new SortedSet<int>();
        foreach (var u in datos.Usuarios.OrderBy(u => u.UsuarioId))
        {
            var tipo = Texto(u.Tipo);
            if (!u.Activo)
            {
                r.Usuarios.Agregar(u.Usuario, tipo, null, "Inactivo: no se migra");
                continue;
            }
            var rol = opciones.Roles?.Rol(u.Tipo);
            if (rol is null)
            {
                tiposSinRol.Add(u.Tipo);
                r.Usuarios.Agregar(u.Usuario, tipo, null, "Su tipo no está en --mapa-roles");
                continue;
            }
            if (Normalizacion.Limpiar(u.Email) is null)
            {
                r.Usuarios.Agregar(u.Usuario, tipo, rol, "Sin correo: no podría recibir la invitación; no se migra");
                continue;
            }
            int? modulo = rol != Roles.Administrador && u.ModuloId is { } m && modulos.ContainsKey(m) ? m : null;
            r.Usuarios.Agregar(u.Usuario, tipo, rol,
                rol != Roles.Administrador && modulo is null ? "Se migra; asignar módulo en el panel" : "Se migra");
            usuarios.Add(new UsuarioPlan(u, rol, modulo));
        }
        return (usuarios, [.. tiposSinRol]);
    }

    private static string Texto(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Los CSV de docs/14 que operación revisa en cada ensayo.</summary>
    private sealed class ReportesPlan
    {
        public Reporte Modulos { get; } = new("modulos.csv", "ModuloAnterior", "Nombre", "Hallazgo");
        public Reporte Tipos { get; } = new("tipos-tramite.csv", "TipoTramite", "TramitesAnteriores", "RequisitoPorReestructurar");
        public Reporte TramitesSinModulo { get; } = new("tramites-sin-modulo.csv", "TramiteAnterior", "Nombre", "ModuloAnterior");
        public Reporte Fusionadas { get; } = new("ofertas-fusionadas.csv", "ModuloAnterior", "TipoTramite", "TramiteConservado", "TramiteFusionado");
        public Reporte Diferencias { get; } = new("diferencias-tramite.csv", "TramiteAnterior", "TipoTramite", "Campo", "ValorDelTramite", "ValorDelTipo");
        public Reporte HorasSospechosas { get; } = new("horas-sospechosas.csv", "TramiteAnterior", "Tramite", "Modulo", "HorarioAnterior", "Hora", "Turnos");
        public Reporte Excepciones { get; } = new("excepciones.csv", "FolioAnterior", "FechaCita", "Motivo");
        public Reporte Duplicados { get; } = new("duplicados-curp.csv", "Curp", "FolioAnterior", "FechaCita", "Hora", "OfertaAnterior", "Resultado");
        public Reporte CurpsInvalidas { get; } = new("curps-invalidas.csv", "FolioAnterior", "Curp", "Resultado");
        public Reporte Sobrecupos { get; } = new("sobrecupos.csv", "OfertaAnterior", "Fecha", "Hora", "Turnos", "Citas");
        public Reporte HistoricoSinOferta { get; } = new("historico-sin-oferta.csv", "Mes", "Citas");
        public Reporte Usuarios { get; } = new("usuarios.csv", "Usuario", "Tipo", "Rol", "Resultado");

        public IReadOnlyList<Reporte> Todos =>
        [
            Modulos, Tipos, TramitesSinModulo, Fusionadas, Diferencias, HorasSospechosas, Excepciones, Duplicados,
            CurpsInvalidas, Sobrecupos, HistoricoSinOferta, Usuarios,
        ];
    }
}
