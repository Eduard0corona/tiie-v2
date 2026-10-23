using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Horarios;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Identidad;
using Tiie.Migracion.Plan;
using Tiie.Migracion.Reportes;

namespace Tiie.Migracion.Destino;

public sealed record ResumenEscritura(
    int ModulosCreados,
    int TiposCreados,
    int OfertasCreadas,
    int OfertasConPlantilla,
    int UsuariosCreados,
    int CitasMigradas,
    int CitasYaMigradas,
    int CitasRechazadas,
    int Historicos,
    ResumenGeneracion Cupos);

/// <summary>
/// Aplica un <see cref="PlanMigracion"/> a la versión 2. Es repetible: reconoce lo ya migrado por
/// <c>IdAnterior</c>, nombre o <c>FolioAnterior</c> y no lo duplica. Los catálogos que ya existen no se
/// sobrescriben, porque operación pudo corregirlos en el panel después de un ensayo.
/// </summary>
public sealed class EscritorDestino(
    TiieDbContext db,
    UserManager<Usuario> usuarios,
    RelojAgenda reloj,
    ITokensCita tokens,
    GeneradorCupos generador,
    IOptions<OpcionesAgenda> agenda)
{
    /// <summary>Lo que el destino no aceptó; se guarda junto a los reportes del plan.</summary>
    public Reporte Rechazos { get; } = new("rechazos-destino.csv", "Tipo", "Clave", "Motivo");

    public async Task<ResumenEscritura> AplicarAsync(PlanMigracion plan, bool reemplazarPlantillas, Action<string> avance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(avance);

        avance("Entidades y municipios");
        await EntidadesYMunicipiosAsync(plan, ct);
        avance("Módulos");
        var (modulos, modulosCreados) = await ModulosAsync(plan, ct);
        avance("Tipos de trámite");
        var (tipos, tiposCreados) = await TiposAsync(plan, ct);
        avance("Ofertas");
        var (ofertas, ofertasCreadas) = await OfertasAsync(plan, modulos, tipos, ct);
        avance("Plantillas");
        var conPlantilla = await PlantillasAsync(plan, ofertas, reemplazarPlantillas, ct);
        avance("Usuarios");
        var usuariosCreados = await UsuariosAsync(plan, modulos, ct);
        avance($"Citas futuras ({plan.CitasAMigrar})");
        var (migradas, yaMigradas, rechazadas) = await CitasAsync(plan, ofertas, ct);
        avance("Totales históricos");
        var historicos = await HistoricosAsync(plan, ofertas, ct);
        avance("Cupos de la ventana a partir de las plantillas");
        var cupos = await generador.GenerarTodasAsync(ct);

        return new ResumenEscritura(modulosCreados, tiposCreados, ofertasCreadas, conPlantilla, usuariosCreados,
            migradas, yaMigradas, rechazadas, historicos, cupos);
    }

    /// <summary>
    /// Encola el «Aviso de migración» de cada cita migrada que aún no lo tiene (docs/14#corte, hora 0 + 75 min).
    /// Va aparte de <c>migrar</c> para que ningún correo salga antes del cambio de DNS.
    /// </summary>
    public async Task<int> EncolarAvisosAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal();
        var pendientes = await db.Citas.AsNoTracking()
            .Where(c => c.Origen == OrigenCita.Migracion && c.Estado == EstadoCita.Agendada && c.Cupo.Fecha >= hoy)
            .Where(c => c.Solicitante.Correo != null)
            .Where(c => !c.Notificaciones.Any(n => n.Tipo == TipoNotificacion.AvisoMigracion))
            .Select(c => new { c.CitaId, Correo = c.Solicitante.Correo! })
            .ToListAsync(ct);
        var ahora = reloj.AhoraUtc();
        foreach (var p in pendientes)
            db.Notificaciones.Add(Notificacion.Nueva(p.CitaId, TipoNotificacion.AvisoMigracion, p.Correo, ahora));
        await db.SaveChangesAsync(ct);
        return pendientes.Count;
    }

    private async Task EntidadesYMunicipiosAsync(PlanMigracion plan, CancellationToken ct)
    {
        var entidades = await db.Entidades.ToDictionaryAsync(e => (int)e.EntidadId, ct);
        foreach (var e in plan.Entidades.Where(e => !entidades.ContainsKey(e.EntidadId)))
            db.Entidades.Add(new Entidad { EntidadId = (byte)e.EntidadId, Nombre = Normalizacion.Limpiar(e.Nombre, 60)!, Activo = e.Activo });
        await db.SaveChangesAsync(ct);

        var municipios = await db.Municipios.Select(m => (int)m.MunicipioId).ToHashSetAsync(ct);
        foreach (var m in plan.Municipios.Where(m => !municipios.Contains(m.MunicipioId)))
            db.Municipios.Add(new Municipio { MunicipioId = (short)m.MunicipioId, EntidadId = (byte)m.EntidadId, Nombre = Normalizacion.Limpiar(m.Nombre, 100)! });
        await db.SaveChangesAsync(ct);
    }

    private async Task<(Dictionary<int, int>, int)> ModulosAsync(PlanMigracion plan, CancellationToken ct)
    {
        var existentes = await db.Modulos.Where(m => m.IdAnterior != null).ToDictionaryAsync(m => m.IdAnterior!.Value, ct);
        var creados = 0;
        foreach (var p in plan.Modulos.Where(p => !existentes.ContainsKey(p.IdAnterior)))
        {
            var modulo = new Modulo
            {
                MunicipioId = p.MunicipioId,
                Nombre = p.Nombre,
                Calle = p.Calle,
                NumeroExterior = p.NumeroExterior,
                NumeroInterior = p.NumeroInterior,
                Colonia = p.Colonia,
                CodigoPostal = p.CodigoPostal,
                Localidad = p.Localidad,
                Telefono = p.Telefono,
                Correo = p.Correo,
                Activo = p.Activo,
                IdAnterior = p.IdAnterior,
            };
            db.Modulos.Add(modulo);
            existentes[p.IdAnterior] = modulo;
            creados++;
        }
        await db.SaveChangesAsync(ct);
        return (existentes.ToDictionary(m => m.Key, m => m.Value.ModuloId), creados);
    }

    private async Task<(Dictionary<string, int>, int)> TiposAsync(PlanMigracion plan, CancellationToken ct)
    {
        // Se comparan por nombre normalizado: un tipo creado o renombrado en el panel con otros acentos es el mismo.
        var existentes = (await db.TiposTramite.ToListAsync(ct))
            .GroupBy(t => Normalizacion.ClaveNombre(t.Nombre))
            .ToDictionary(g => g.Key, g => g.First());
        var creados = 0;
        foreach (var p in plan.Tipos.Where(p => !existentes.ContainsKey(p.Clave)))
        {
            var tipo = new TipoTramite { Nombre = p.Nombre, Descripcion = p.Descripcion, Costo = p.Costo, Notas = p.Notas };
            // Un solo requisito sin nivel escolar: operación lo reestructura por nivel en el panel (tipos-tramite.csv).
            if (p.Requisito is not null)
                tipo.Requisitos.Add(new Requisito { Texto = p.Requisito, Orden = 1 });
            db.TiposTramite.Add(tipo);
            existentes[p.Clave] = tipo;
            creados++;
        }
        await db.SaveChangesAsync(ct);
        return (existentes.ToDictionary(t => t.Key, t => t.Value.TipoTramiteId), creados);
    }

    private async Task<(Dictionary<int, int>, int)> OfertasAsync(
        PlanMigracion plan, Dictionary<int, int> modulos, Dictionary<string, int> tipos, CancellationToken ct)
    {
        var todas = await db.Ofertas.ToListAsync(ct);
        var porAnterior = todas.Where(o => o.IdAnterior != null).ToDictionary(o => o.IdAnterior!.Value);
        var porModuloYTipo = todas.ToDictionary(o => (o.ModuloId, o.TipoTramiteId));
        var creadas = 0;
        foreach (var p in plan.Ofertas.Where(p => !porAnterior.ContainsKey(p.IdAnterior)))
        {
            var clave = (modulos[p.ModuloIdAnterior], tipos[p.ClaveTipo]);
            if (porModuloYTipo.TryGetValue(clave, out var existente))
            {
                // Se creó en el panel antes de migrar: se enlaza con el trámite anterior en lugar de duplicarla.
                existente.IdAnterior ??= p.IdAnterior;
                porAnterior[p.IdAnterior] = existente;
                Rechazos.Agregar("Oferta", p.IdAnterior.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Ya existía en la versión 2 para ese módulo y tipo: se enlazó sin cambiar sus datos");
                continue;
            }
            var oferta = new Oferta
            {
                ModuloId = clave.Item1,
                TipoTramiteId = clave.Item2,
                DiasVentana = agenda.Value.DiasVentanaPredeterminados,
                NotasSede = p.NotasSede,
                Activo = p.Activo,
                IdAnterior = p.IdAnterior,
            };
            db.Ofertas.Add(oferta);
            porAnterior[p.IdAnterior] = oferta;
            porModuloYTipo[clave] = oferta;
            creadas++;
        }
        await db.SaveChangesAsync(ct);
        return (porAnterior.ToDictionary(o => o.Key, o => o.Value.OfertaId), creadas);
    }

    private async Task<int> PlantillasAsync(PlanMigracion plan, Dictionary<int, int> ofertas, bool reemplazar, CancellationToken ct)
    {
        var conPlantilla = await db.Plantillas.Select(p => p.OfertaId).Distinct().ToHashSetAsync(ct);
        var escritas = 0;
        foreach (var grupo in plan.Plantillas.GroupBy(p => ofertas[p.OfertaIdAnterior]))
        {
            if (conPlantilla.Contains(grupo.Key))
            {
                if (!reemplazar)
                    continue;
                await db.Plantillas.Where(p => p.OfertaId == grupo.Key).ExecuteDeleteAsync(ct);
            }
            db.Plantillas.AddRange(grupo.Select(p => new PlantillaHorario { OfertaId = grupo.Key, DiaSemana = p.Dia, Hora = p.Hora, Turnos = p.Turnos }));
            escritas++;
        }
        await db.SaveChangesAsync(ct);
        return escritas;
    }

    private async Task<int> UsuariosAsync(PlanMigracion plan, Dictionary<int, int> modulos, CancellationToken ct)
    {
        var creados = 0;
        foreach (var p in plan.Usuarios)
        {
            var u = p.Usuario;
            if (await usuarios.FindByNameAsync(u.Usuario) is not null)
                continue;
            // Sin contraseña: cada persona la define con la invitación. Las anteriores no se reutilizan (docs/14#usuarios).
            var usuario = new Usuario
            {
                UserName = u.Usuario.Trim(),
                Email = Normalizacion.Limpiar(u.Email, 256),
                Nombre = Normalizacion.Limpiar(u.Nombre, 80) ?? u.Usuario,
                ApellidoPaterno = Normalizacion.Limpiar(u.ApellidoPaterno, 80) ?? "Sin dato",
                ApellidoMaterno = Normalizacion.Limpiar(u.ApellidoMaterno, 80),
                EmailConfirmed = false,
            };
            var creado = await usuarios.CreateAsync(usuario);
            if (!creado.Succeeded)
            {
                Rechazos.Agregar("Usuario", u.Usuario, string.Join(" ", creado.Errors.Select(e => e.Description)));
                continue;
            }
            await usuarios.AddToRoleAsync(usuario, p.Rol);
            if (p.ModuloIdAnterior is { } modulo && p.Rol != Roles.Administrador)
            {
                db.UsuariosModulos.Add(new UsuarioModulo { UsuarioId = usuario.Id, ModuloId = modulos[modulo] });
                await db.SaveChangesAsync(ct);
            }
            creados++;
        }
        return creados;
    }

    private async Task<(int Migradas, int YaMigradas, int Rechazadas)> CitasAsync(PlanMigracion plan, Dictionary<int, int> ofertas, CancellationToken ct)
    {
        var yaMigradas = (await db.Citas.Where(c => c.FolioAnterior != null).Select(c => c.FolioAnterior!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (migradas, existentes, rechazadas) = (0, 0, 0);

        foreach (var cupo in plan.Cupos)
        {
            var pendientes = cupo.Citas.Where(c => !yaMigradas.Contains(c.Cita.Folio)).ToList();
            existentes += cupo.Citas.Count - pendientes.Count;
            if (pendientes.Count == 0)
                continue;

            // RN-01 en el destino: quien ya tiene otra cita Agendada (por ejemplo, de un ensayo) no recibe esta.
            var curps = pendientes.Where(p => p.Curp is not null).Select(p => p.Curp!).ToList();
            var conCita = (await db.Citas
                    .Where(c => c.Estado == EstadoCita.Agendada && c.Solicitante.Curp != null && curps.Contains(c.Solicitante.Curp))
                    .Select(c => c.Solicitante.Curp!)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var p in pendientes.Where(p => p.Curp is not null && conCita.Contains(p.Curp)))
                Rechazos.Agregar("Cita", p.Cita.Folio, "La CURP ya tiene una cita Agendada en la versión 2 (RN-01)");
            var aMigrar = pendientes.Where(p => p.Curp is null || !conCita.Contains(p.Curp)).ToList();
            rechazadas += pendientes.Count - aMigrar.Count;
            if (aMigrar.Count == 0)
                continue;

            var ofertaId = ofertas[cupo.OfertaIdAnterior];
            // Cupo y citas en la misma transacción; Ocupados cambia con un UPDATE condicional (regla 4 de CLAUDE.md).
            await db.EnTransaccionAsync(async ct2 =>
            {
                var cupoId = await AsegurarCupoAsync(ofertaId, cupo, ct2);
                var n = (short)aMigrar.Count;
                await db.Cupos.Where(c => c.CupoId == cupoId).ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Capacidad, c => c.Ocupados + n > c.Capacidad ? (short)(c.Ocupados + n) : c.Capacidad)
                    .SetProperty(c => c.Ocupados, c => (short)(c.Ocupados + n)), ct2);

                var ahora = reloj.AhoraUtc();
                foreach (var p in aMigrar)
                {
                    var solicitante = await SolicitanteAsync(p, ct2);
                    var folio = await Folios.SiguienteAsync(db, reloj.HoyLocal(), ct2);
                    // Sin aviso de privacidad aceptado: la persona lo acepta si reprograma (docs/14#citas-futuras).
                    var cita = Cita.Agendar(folio, cupoId, solicitante, OrigenCita.Migracion, tokens.Emitir(cupo.Fecha), ahora);
                    cita.FolioAnterior = p.Cita.Folio;
                    db.Citas.Add(cita);
                }
                await db.SaveChangesAsync(ct2);
                return 0;
            }, ct);
            db.ChangeTracker.Clear();
            migradas += aMigrar.Count;
        }
        return (migradas, existentes, rechazadas);
    }

    private async Task<int> AsegurarCupoAsync(int ofertaId, CupoPlan plan, CancellationToken ct)
    {
        var cupoId = await db.Cupos
            .Where(c => c.OfertaId == ofertaId && c.Fecha == plan.Fecha && c.Hora == plan.Hora)
            .Select(c => (int?)c.CupoId)
            .SingleOrDefaultAsync(ct);
        if (cupoId is { } id)
            return id;
        var cupo = new Cupo { OfertaId = ofertaId, Fecha = plan.Fecha, Hora = plan.Hora, Capacidad = plan.Turnos };
        db.Cupos.Add(cupo);
        await db.SaveChangesAsync(ct);
        return cupo.CupoId;
    }

    private async Task<Solicitante> SolicitanteAsync(CitaPlan p, CancellationToken ct)
    {
        var c = p.Cita;
        var datos = new DatosSolicitante(
            Normalizacion.Limpiar(c.Nombre, 80) ?? "Sin dato",
            Normalizacion.Limpiar(c.ApellidoPaterno, 80) ?? "Sin dato",
            Normalizacion.Limpiar(c.ApellidoMaterno, 80),
            Normalizacion.Digitos(c.Celular, 10),
            Normalizacion.Limpiar(c.Email, 150));
        if (p.CurpValida)
            return await Solicitantes.ObtenerOActualizarAsync(db, Curp.Interpretar(p.Curp!), datos, ct);

        // CURP que no pasa la validación (curps-invalidas.csv): se guarda tal cual, sin fecha de nacimiento ni sexo.
        var existente = p.Curp is null ? null : await db.Solicitantes.SingleOrDefaultAsync(s => s.Curp == p.Curp, ct);
        var solicitante = existente ?? new Solicitante { Curp = p.Curp, Nombre = datos.Nombre, ApellidoPaterno = datos.ApellidoPaterno };
        solicitante.ApellidoMaterno = datos.ApellidoMaterno;
        solicitante.Celular = datos.Celular ?? solicitante.Celular;
        solicitante.Correo = datos.Correo ?? solicitante.Correo;
        if (existente is null)
            db.Solicitantes.Add(solicitante);
        return solicitante;
    }

    private async Task<int> HistoricosAsync(PlanMigracion plan, Dictionary<int, int> ofertas, CancellationToken ct)
    {
        // Los totales se reemplazan: la base anterior es la fuente y vuelve a contar en cada corrida.
        var existentes = await db.HistoricoCitas.ToDictionaryAsync(h => (h.Fecha, h.OfertaId), ct);
        foreach (var p in plan.Historicos)
        {
            var clave = (p.Fecha, ofertas[p.OfertaIdAnterior]);
            if (existentes.TryGetValue(clave, out var historico))
                historico.Total = p.Total;
            else
                db.HistoricoCitas.Add(existentes[clave] = new HistoricoCita { Fecha = p.Fecha, OfertaId = clave.Item2, Total = p.Total });
        }
        await db.SaveChangesAsync(ct);
        return plan.Historicos.Count;
    }
}
