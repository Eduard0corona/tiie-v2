using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Horarios;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;

namespace Tiie.Aplicacion.Procesos;

/// <summary>Crea y ajusta los cupos de toda la ventana de cada oferta activa (RN-02, RN-04, RN-07).</summary>
public sealed class GeneracionCupos(GeneradorCupos generador, IOptions<OpcionesAgenda> agenda) : IProcesoDiario
{
    public string Nombre => nameof(GeneracionCupos);
    public TimeOnly Hora => agenda.Value.HoraGeneracionCupos;
    public DayOfWeek? Dia => null;

    public async Task<string> EjecutarAsync(CancellationToken ct)
    {
        var r = await generador.GenerarTodasAsync(ct);
        return string.Create(CultureInfo.InvariantCulture,
            $"Creados {r.CuposCreados}, actualizados {r.CuposActualizados}, eliminados {r.CuposEliminados}, con citas sin cambio {r.CuposConCitasSinCambio}.");
    }
}

/// <summary>RN-12: toda cita Agendada con fecha anterior a hoy pasa a No asistió, sin usuario (sistema).</summary>
public sealed class CierreDiario(ITiieDb db, RelojAgenda reloj, IOptions<OpcionesAgenda> agenda) : IProcesoDiario
{
    public string Nombre => nameof(CierreDiario);
    public TimeOnly Hora => agenda.Value.HoraCierreDiario;
    public DayOfWeek? Dia => null;

    public async Task<string> EjecutarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal();
        var ahora = reloj.AhoraUtc();
        var cerradas = await db.Citas
            .Where(c => c.Estado == EstadoCita.Agendada && c.Cupo.Fecha < hoy)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, EstadoCita.NoAsistio)
                .SetProperty(c => c.ActualizadaEn, ahora)
                .SetProperty(c => c.ActualizadaPor, (int?)null), ct);
        return string.Create(CultureInfo.InvariantCulture, $"{cerradas} citas pasaron a No asistió.");
    }
}

/// <summary>
/// RN-14: encola un recordatorio para cada cita Agendada de mañana con correo. No se envía si la cita se creó
/// después de la hora del recordatorio del día anterior: la confirmación es suficiente.
/// </summary>
public sealed class Recordatorios(ITiieDb db, RelojAgenda reloj, IOptions<OpcionesAgenda> agenda) : IProcesoDiario
{
    public string Nombre => nameof(Recordatorios);
    public TimeOnly Hora => agenda.Value.HoraRecordatorio;
    public DayOfWeek? Dia => null;

    public async Task<string> EjecutarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal();
        var manana = hoy.AddDays(1);
        var corte = reloj.AUtc(hoy, Hora);
        var ahora = reloj.AhoraUtc();

        var citas = await db.Citas.AsNoTracking()
            .Where(c => c.Estado == EstadoCita.Agendada && c.Cupo.Fecha == manana && c.Solicitante.Correo != null && c.CreadaEn < corte)
            .Where(c => !c.Notificaciones.Any(n => n.Tipo == TipoNotificacion.Recordatorio))
            .Select(c => new { c.CitaId, Correo = c.Solicitante.Correo! })
            .ToListAsync(ct);

        db.Notificaciones.AddRange(citas.Select(c => Notificacion.Nueva(c.CitaId, TipoNotificacion.Recordatorio, c.Correo, ahora)));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.ViolaIndice("UX_Notificacion_Recordatorio"))
        {
            // Otra ejecución ya los encoló; el índice evita duplicados (RN-14).
            return "Los recordatorios ya estaban encolados.";
        }
        return string.Create(CultureInfo.InvariantCulture, $"{citas.Count} recordatorios encolados para {manana:yyyy-MM-dd}.");
    }
}

/// <summary>
/// Anonimiza solicitantes cuya última cita pasó hace más de <c>Agenda:MesesRetencion</c> meses y borra las
/// notificaciones enviadas de más de 180 días (docs/11#retención-y-anonimización). Desactivada mientras el
/// plazo no se defina.
/// </summary>
public sealed class Retencion(ITiieDb db, RelojAgenda reloj, IOptions<OpcionesAgenda> agenda) : IProcesoDiario
{
    public const string TextoAnonimizado = "ANONIMIZADO";
    public static readonly TimeSpan VidaNotificacionesEnviadas = TimeSpan.FromDays(180);

    /// <summary>Filas por sentencia al borrar; por debajo de las 5 000 en que SQL Server escala a candado de tabla.</summary>
    public const int LoteBorrado = 4000;

    public string Nombre => nameof(Retencion);
    public TimeOnly Hora => new(2, 0);
    public DayOfWeek? Dia => DayOfWeek.Sunday;

    public async Task<string> EjecutarAsync(CancellationToken ct)
    {
        if (agenda.Value.MesesRetencion is not { } meses)
            return "Desactivada: Agenda:MesesRetencion está vacío.";

        var limite = reloj.HoyLocal().AddMonths(-meses);
        var vencidos = await db.Solicitantes.AsNoTracking()
            .Where(s => s.AnonimizadoEn == null)
            .Where(s => !db.Citas.Any(c => c.SolicitanteId == s.SolicitanteId && (c.Estado == EstadoCita.Agendada || c.Cupo.Fecha >= limite)))
            .Select(s => s.SolicitanteId)
            .ToListAsync(ct);
        var anonimizados = await AnonimizarAsync(vencidos, ct);

        var borradas = await BorrarNotificacionesEnviadasAsync(reloj.AhoraUtc() - VidaNotificacionesEnviadas, ct);
        return string.Create(CultureInfo.InvariantCulture, $"{anonimizados} solicitantes anonimizados; {borradas} notificaciones borradas.");
    }

    /// <summary>
    /// Borra por lotes, cada uno en su propia sentencia: un solo DELETE grande escalaría a un candado de toda la tabla
    /// y detendría las reservas, que insertan su confirmación en <c>Notificacion</c> dentro de su transacción. Cada
    /// lote sigue donde terminó el anterior por la llave primaria, para no recorrer otra vez las filas ya revisadas.
    /// </summary>
    private async Task<int> BorrarNotificacionesEnviadasAsync(DateTimeOffset antes, CancellationToken ct)
    {
        var total = 0;
        var ultimo = 0L;
        while (true)
        {
            var lote = await db.Notificaciones.AsNoTracking()
                .Where(n => n.NotificacionId > ultimo && n.Estado == EstadoNotificacion.Enviada && n.EnviadaEn < antes)
                .OrderBy(n => n.NotificacionId)
                .Select(n => n.NotificacionId)
                .Take(LoteBorrado)
                .ToListAsync(ct);
            if (lote.Count == 0)
                return total;

            var (primero, final) = (lote[0], lote[^1]);
            total += await db.Notificaciones
                .Where(n => n.NotificacionId >= primero && n.NotificacionId <= final
                    && n.Estado == EstadoNotificacion.Enviada && n.EnviadaEn < antes)
                .ExecuteDeleteAsync(ct);
            ultimo = final;
        }
    }

    /// <summary>
    /// Quita CURP, nombre y contacto. Las citas se conservan para estadística. Lo usan también las solicitudes
    /// ARCO que atiende un administrador.
    /// </summary>
    public async Task<int> AnonimizarAsync(IReadOnlyCollection<int> solicitanteIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitanteIds);
        var ahora = reloj.AhoraUtc();
        var total = 0;
        foreach (var lote in solicitanteIds.Chunk(500))
        {
            total += await db.Solicitantes.Where(s => lote.Contains(s.SolicitanteId)).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Curp, (string?)null)
                .SetProperty(x => x.Celular, (string?)null)
                .SetProperty(x => x.Correo, (string?)null)
                .SetProperty(x => x.Nombre, TextoAnonimizado)
                .SetProperty(x => x.ApellidoPaterno, TextoAnonimizado)
                .SetProperty(x => x.ApellidoMaterno, (string?)null)
                .SetProperty(x => x.AnonimizadoEn, ahora), ct);
            await db.Notificaciones.Where(n => lote.Contains(n.Cita.SolicitanteId))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Destinatario, TextoAnonimizado), ct);
        }
        return total;
    }
}
