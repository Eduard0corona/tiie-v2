using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Privacidad;

namespace Tiie.Aplicacion.Procesos;

/// <summary>
/// Envía la bandeja de salida (docs/10#proceso-de-envío). Reserva un lote con una sola sentencia; <c>READPAST</c>
/// salta filas que otra instancia ya tomó, así que dos instancias nunca envían el mismo correo.
/// </summary>
public sealed class EnvioNotificaciones(
    ITiieDb db,
    ICorreo correo,
    IPlantillasCorreo plantillas,
    IGeneradorPdf pdf,
    ITokensCita tokens,
    RelojAgenda reloj,
    IOptions<OpcionesSitio> sitio,
    Metricas metricas,
    ILogger<EnvioNotificaciones> logger)
{
    public const int Lote = 20;

    /// <summary>Si la instancia se cae a mitad del lote, otra retoma esas notificaciones al vencer la reserva.</summary>
    public static readonly TimeSpan Reserva = TimeSpan.FromMinutes(5);

    /// <summary>Una notificación vencida hace más de este tiempo cuenta como atrasada.</summary>
    public static readonly TimeSpan Atraso = TimeSpan.FromMinutes(15);

    /// <returns>Cuántas notificaciones procesó en esta pasada.</returns>
    public async Task<int> EjecutarAsync(CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc();
        var reservadaHasta = ahora + Reserva;
        var ids = await db.Database.SqlQuery<long>($"""
            UPDATE TOP ({Lote}) dbo.Notificacion WITH (READPAST, UPDLOCK, ROWLOCK)
            SET ProximoIntentoEn = {reservadaHasta}
            OUTPUT inserted.NotificacionId AS Value
            WHERE Estado IN (1, 3) AND ProximoIntentoEn <= {ahora}
            """).ToListAsync(ct);

        foreach (var id in ids)
            await EnviarAsync(id, ct);

        // Vencidas hace más de 15 minutos: la bandeja no avanza (alerta de docs/13#monitoreo-y-alertas).
        var atrasoDesde = ahora - Atraso;
        metricas.NotificacionesAtrasadas(await db.Notificaciones.CountAsync(
            n => (n.Estado == EstadoNotificacion.Pendiente || n.Estado == EstadoNotificacion.Fallida) && n.ProximoIntentoEn <= atrasoDesde, ct));
        return ids.Count;
    }

    private async Task EnviarAsync(long notificacionId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var notificacion = await db.Notificaciones.SingleAsync(n => n.NotificacionId == notificacionId, ct);
        var cita = await ConsultasCita.ExigirAsync(db, notificacion.CitaId, ct);
        var ahora = reloj.AhoraUtc();

        if (notificacion.Tipo is TipoNotificacion.Confirmacion or TipoNotificacion.Recordatorio && cita.Estado != EstadoCita.Agendada)
        {
            notificacion.Descartar("La cita ya no está agendada.");
            await db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var mensaje = Armar(notificacion, cita);
            await correo.EnviarAsync(mensaje, ct);
            notificacion.MarcarEnviada(ahora);
            logger.LogInformation("Notificación {NotificacionId} ({Tipo}) de la cita {Folio} enviada a {Destinatario}",
                notificacion.NotificacionId, notificacion.Tipo, cita.Folio, Enmascarar.Correo(notificacion.Destinatario));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El mensaje del proveedor puede incluir el destinatario: solo se guarda el tipo de error.
            var error = ex is CorreoNoEnviadoException or EnlaceNoDisponibleException ? ex.Message : ex.GetType().Name;
            if (notificacion.RegistrarFallo(error, ahora))
            {
                metricas.NotificacionDescartada(notificacion.Tipo.ToString());
                logger.LogWarning("Notificación {NotificacionId} ({Tipo}) de la cita {Folio} descartada tras {Intentos} intentos",
                    notificacion.NotificacionId, notificacion.Tipo, cita.Folio, notificacion.Intentos);
            }
            else
            {
                logger.LogInformation(ex, "Notificación {NotificacionId} falló; se reintenta en {ProximoIntento}",
                    notificacion.NotificacionId, notificacion.ProximoIntentoEn);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private MensajeCorreo Armar(Notificacion notificacion, DatosCita cita)
    {
        var conEnlaceACita = notificacion.Tipo is not (TipoNotificacion.Cancelacion or TipoNotificacion.CierrePorBloqueo);
        var enlace = conEnlaceACita
            ? $"{sitio.Value.UrlBase}/mi-cita/{tokens.Recuperar(cita.TokenProtegido) ?? throw new EnlaceNoDisponibleException()}"
            : $"{sitio.Value.UrlBase}/agendar";

        var datos = new DatosNotificacion(
            cita.Nombre,
            cita.Folio,
            Formatos.FechaLarga(cita.Fecha),
            Formatos.Hora(cita.Hora),
            cita.Modulo,
            cita.Direccion,
            cita.Tramite,
            [.. cita.Requisitos.Select(r => r.NivelEscolar is null ? r.Texto : $"{r.NivelEscolar}: {r.Texto}")],
            enlace,
            cita.MotivoCambio,
            $"{sitio.Value.UrlBase}/aviso-de-privacidad");
        var mensaje = plantillas.Notificacion(notificacion.Tipo, notificacion.Destinatario, datos);

        // El comprobante se genera al enviar, con los datos vigentes de la cita.
        if (notificacion.Tipo is TipoNotificacion.Confirmacion or TipoNotificacion.Reprogramacion or TipoNotificacion.AvisoMigracion)
            mensaje = mensaje with { Adjuntos = [new AdjuntoCorreo($"comprobante-{cita.Folio}.pdf", "application/pdf", pdf.Comprobante(cita))] };
        return mensaje;
    }
}

/// <summary>El token protegido no se pudo descifrar: se perdieron las llaves de Data Protection.</summary>
public sealed class EnlaceNoDisponibleException : Exception
{
    public EnlaceNoDisponibleException()
        : base("No se pudo recuperar el token de la cita; revisa las llaves de Data Protection.") { }

    public EnlaceNoDisponibleException(string message)
        : base(message) { }

    public EnlaceNoDisponibleException(string message, Exception innerException)
        : base(message, innerException) { }
}
