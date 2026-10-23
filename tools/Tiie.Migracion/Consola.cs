using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Reloj;
using Tiie.Infraestructura.Datos;
using Tiie.Migracion.Destino;
using Tiie.Migracion.Origen;
using Tiie.Migracion.Plan;
using Tiie.Migracion.Reportes;

namespace Tiie.Migracion;

/// <summary>Comandos de docs/14-migracion-de-datos.md#herramienta.</summary>
public static class Consola
{
    public const string Ayuda = """
        Tiie.Migracion: migración desde la base de 2016 (docs/14-migracion-de-datos.md).

          analizar  --origen <cadena> [--fecha-corte aaaa-mm-dd] [--mapa-roles <mapa>] [--decisiones <csv>]
                    Diagnóstico y reportes. No escribe en ninguna base.
          migrar    --origen <cadena> --destino <cadena> --fecha-corte aaaa-mm-dd --mapa-roles "0=Capturista,1=Supervisor,2=Administrador"
                    [--decisiones <csv>] [--reemplazar-plantillas] [--llaves-locales]
                    Repetible: no duplica lo ya migrado ni sobrescribe catálogos que ya existen.
          conciliar --origen <cadena> --destino <cadena> --fecha-corte aaaa-mm-dd --mapa-roles <mapa> [--decisiones <csv>]
                    Compara conteos; termina con código 1 si hay diferencias.
          avisar    --destino <cadena>
                    Encola el «Aviso de migración» de las citas migradas. Solo después del cambio de DNS.

          --salida <carpeta>   Reportes; por omisión artifacts/migracion/<fecha y hora>.
          --llaves-locales     Protege los tokens con las llaves del perfil local, solo para un destino local.
                               Para Azure, define DataProtection__BlobUri y DataProtection__KeyUri como en el sitio.

        Los reportes contienen datos personales: genéralos solo en un equipo autorizado y bórralos después del corte.
        """;

    public static async Task<int> EjecutarAsync(string[] args, TextWriter salida, TextWriter errores, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(salida);
        ArgumentNullException.ThrowIfNull(errores);
        if (args.Length == 0 || args[0] is "-h" or "--help" or "ayuda")
        {
            await salida.WriteLineAsync(Ayuda);
            return args.Length == 0 ? 2 : 0;
        }
        try
        {
            var a = Argumentos.Parsear(args.Skip(1));
            return args[0] switch
            {
                "analizar" => await AnalizarAsync(a, salida, ct),
                "migrar" => await MigrarAsync(a, salida, errores, ct),
                "conciliar" => await ConciliarAsync(a, salida, ct),
                "avisar" => await AvisarAsync(a, salida, errores, ct),
                _ => throw new FormatException($"Comando desconocido: {args[0]}. Usa analizar, migrar, conciliar o avisar."),
            };
        }
        catch (FormatException ex)
        {
            await errores.WriteLineAsync(ex.Message);
            return 2;
        }
    }

    private static async Task<int> AnalizarAsync(Argumentos a, TextWriter salida, CancellationToken ct)
    {
        var lector = new LectorOrigen(a.Exigir("origen"));
        if (!await EsquemaCoincideAsync(lector, salida, ct))
            return 1;
        var corte = a.Fecha("fecha-corte") ?? Reloj().HoyLocal();
        var datos = await lector.LeerAsync(corte, ct);
        var plan = Planificador.Crear(datos, Opciones(a, corte, exigirRoles: false));

        await salida.WriteLineAsync($"Análisis con fecha de corte {Fecha(corte)}");
        await ImprimirPlanAsync(plan, salida);
        var tipos = datos.Usuarios.Where(u => u.Activo).GroupBy(u => u.Tipo).OrderBy(g => g.Key)
            .Select(g => $"{g.Key} ({g.Count()} usuarios)");
        await salida.WriteLineAsync($"  Valores de Usuario.Tipo en usuarios activos: {string.Join(", ", tipos)}");
        await salida.WriteLineAsync($"Reportes en {await GuardarAsync(a, plan.Reportes, ct)}");
        return 0;
    }

    private static async Task<int> MigrarAsync(Argumentos a, TextWriter salida, TextWriter errores, CancellationToken ct)
    {
        var lector = new LectorOrigen(a.Exigir("origen"));
        var destino = a.Exigir("destino");
        var corte = a.Fecha("fecha-corte") ?? throw new FormatException("Falta --fecha-corte.");
        var opciones = Opciones(a, corte, exigirRoles: true);
        if (!Servicios.LlavesConfiguradas().Configurado && !a.Bandera("llaves-locales"))
        {
            await errores.WriteLineAsync(
                "Los tokens de las citas deben protegerse con las llaves del sitio. Define DataProtection__BlobUri y " +
                "DataProtection__KeyUri como en el sitio, o usa --llaves-locales si el destino es local.");
            return 2;
        }
        if (!await EsquemaCoincideAsync(lector, salida, ct))
            return 1;

        var datos = await lector.LeerAsync(corte, ct);
        var plan = Planificador.Crear(datos, opciones);
        if (plan.TiposSinRol.Count > 0)
        {
            await errores.WriteLineAsync($"--mapa-roles no cubre los valores de Usuario.Tipo: {string.Join(", ", plan.TiposSinRol)}.");
            return 2;
        }
        await ImprimirPlanAsync(plan, salida);

        await using var servicios = Servicios.Crear(destino);
        await using var alcance = servicios.CreateAsyncScope();
        if (!await DestinoAlDiaAsync(alcance.ServiceProvider, errores, ct))
            return 1;
        var escritor = ActivatorUtilities.CreateInstance<EscritorDestino>(alcance.ServiceProvider);
        var resumen = await escritor.AplicarAsync(plan, a.Bandera("reemplazar-plantillas"), m => salida.WriteLine($"· {m}"), ct);

        await salida.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"""
            Migración terminada:
              Creados: {resumen.ModulosCreados} módulos, {resumen.TiposCreados} tipos de trámite, {resumen.OfertasCreadas} ofertas, {resumen.UsuariosCreados} usuarios
              Ofertas con plantilla escrita: {resumen.OfertasConPlantilla}
              Citas: {resumen.CitasMigradas} migradas, {resumen.CitasYaMigradas} ya estaban, {resumen.CitasRechazadas} rechazadas
              Totales históricos: {resumen.Historicos}
              Cupos de la ventana: {resumen.Cupos.CuposCreados} creados, {resumen.Cupos.CuposActualizados} ajustados
            """));
        await salida.WriteLineAsync($"Reportes en {await GuardarAsync(a, [.. plan.Reportes, escritor.Rechazos], ct)}");
        await salida.WriteLineAsync("Siguiente paso: conciliar. Los avisos a los ciudadanos se encolan con «avisar» después del cambio de DNS.");
        return 0;
    }

    private static async Task<int> ConciliarAsync(Argumentos a, TextWriter salida, CancellationToken ct)
    {
        var lector = new LectorOrigen(a.Exigir("origen"));
        var destino = a.Exigir("destino");
        var corte = a.Fecha("fecha-corte") ?? throw new FormatException("Falta --fecha-corte.");
        var opciones = Opciones(a, corte, exigirRoles: true);
        if (!await EsquemaCoincideAsync(lector, salida, ct))
            return 1;
        var datos = await lector.LeerAsync(corte, ct);
        var plan = Planificador.Crear(datos, opciones);

        await using var servicios = Servicios.Crear(destino);
        await using var alcance = servicios.CreateAsyncScope();
        var conciliador = ActivatorUtilities.CreateInstance<Conciliador>(alcance.ServiceProvider);
        var verificaciones = await conciliador.ConciliarAsync(datos, plan, corte, ct);

        await salida.WriteLineAsync($"Conciliación con fecha de corte {Fecha(corte)}");
        foreach (var v in verificaciones)
        {
            await salida.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"  {(v.Correcta ? "ok        " : "DIFERENCIA")} {v.Nombre}: origen {v.Origen}, esperado {v.Esperado}, destino {v.Destino}"));
            if (v.Origen != v.Esperado || !v.Correcta)
                await salida.WriteLineAsync($"             {v.Explicacion}");
        }
        await salida.WriteLineAsync($"Reportes en {await GuardarAsync(a, [.. plan.Reportes, Conciliador.Reporte(verificaciones)], ct)}");
        return verificaciones.All(v => v.Correcta) ? 0 : 1;
    }

    private static async Task<int> AvisarAsync(Argumentos a, TextWriter salida, TextWriter errores, CancellationToken ct)
    {
        // Solo inserta notificaciones: el sitio arma el enlace con sus propias llaves al enviarlas.
        await using var servicios = Servicios.Crear(a.Exigir("destino"));
        await using var alcance = servicios.CreateAsyncScope();
        if (!await DestinoAlDiaAsync(alcance.ServiceProvider, errores, ct))
            return 1;
        var escritor = ActivatorUtilities.CreateInstance<EscritorDestino>(alcance.ServiceProvider);
        var encolados = await escritor.EncolarAvisosAsync(ct);
        await salida.WriteLineAsync($"{encolados} avisos de migración encolados. Vigila la bandeja de salida en el panel.");
        return 0;
    }

    private static OpcionesPlan Opciones(Argumentos a, DateOnly corte, bool exigirRoles)
    {
        var mapa = exigirRoles ? a.Exigir("mapa-roles") : a.Opcional("mapa-roles");
        return new OpcionesPlan(
            corte,
            mapa is null ? null : MapaRoles.Parsear(mapa),
            a.Opcional("decisiones") is { } decisiones ? Decisiones.Leer(decisiones) : Decisiones.Vacias);
    }

    private static async Task<bool> EsquemaCoincideAsync(LectorOrigen lector, TextWriter salida, CancellationToken ct)
    {
        var faltantes = await lector.ColumnasFaltantesAsync(ct);
        if (faltantes.Count == 0)
            return true;
        await salida.WriteLineAsync("La base de origen no coincide con el esquema supuesto (tools/Tiie.Migracion/Origen/esquema-supuesto.sql).");
        await salida.WriteLineAsync("Ajusta Origen/LectorOrigen.cs al script real. Faltan:");
        foreach (var columna in faltantes)
            await salida.WriteLineAsync($"  {columna}");
        return false;
    }

    private static async Task<bool> DestinoAlDiaAsync(IServiceProvider servicios, TextWriter errores, CancellationToken ct)
    {
        var pendientes = await servicios.GetRequiredService<TiieDbContext>().Database.GetPendingMigrationsAsync(ct);
        if (!pendientes.Any())
            return true;
        await errores.WriteLineAsync($"El destino tiene migraciones de esquema pendientes ({string.Join(", ", pendientes)}). Aplícalas primero con efbundle.");
        return false;
    }

    private static async Task ImprimirPlanAsync(PlanMigracion plan, TextWriter salida)
    {
        await salida.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"""
              Módulos: {plan.Modulos.Count} ({plan.Modulos.Count(m => m.Activo)} activos)
              Tipos de trámite: {plan.Tipos.Count}; ofertas: {plan.Ofertas.Count}; filas de plantilla: {plan.Plantillas.Count}
              Citas vigentes futuras: {plan.CitasFuturasOrigen}; se migran {plan.CitasAMigrar} en {plan.Cupos.Count} cupos
              Totales históricos: {plan.Historicos.Count} (fecha y oferta)
              Usuarios a migrar: {plan.Usuarios.Count}
            """));
        foreach (var r in plan.Reportes.Where(r => r.Filas.Count > 0 && r.Archivo is not ("tipos-tramite.csv" or "usuarios.csv")))
            await salida.WriteLineAsync($"  Revisar {r.Archivo}: {r.Filas.Count} filas");
    }

    private static async Task<string> GuardarAsync(Argumentos a, IEnumerable<Reporte> reportes, CancellationToken ct)
    {
        var carpeta = a.Opcional("salida")
            ?? Path.Combine("artifacts", "migracion", Reloj().AhoraLocal().ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(carpeta);
        foreach (var reporte in reportes)
            await reporte.GuardarAsync(carpeta, ct);
        return Path.GetFullPath(carpeta);
    }

    private static RelojAgenda Reloj() => new(TimeProvider.System, Options.Create(new OpcionesAgenda()));

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
