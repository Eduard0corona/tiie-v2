using Tiie.Dominio.Citas;

namespace Tiie.Dominio.Notificaciones;

/// <summary>Mensaje en la bandeja de salida. Se inserta en la transacción de la cita (ADR 0007).</summary>
public sealed class Notificacion
{
    /// <summary>Al sexto fallo la notificación se descarta.</summary>
    public const int IntentosMaximos = 6;

    public long NotificacionId { get; set; }
    public int CitaId { get; set; }
    public Cita Cita { get; set; } = null!;
    public TipoNotificacion Tipo { get; set; }
    public CanalNotificacion Canal { get; set; } = CanalNotificacion.Correo;
    public required string Destinatario { get; set; }
    public EstadoNotificacion Estado { get; set; } = EstadoNotificacion.Pendiente;
    public byte Intentos { get; set; }
    public DateTimeOffset ProximoIntentoEn { get; set; }
    public DateTimeOffset? EnviadaEn { get; set; }
    /// <summary>Sin datos personales.</summary>
    public string? UltimoError { get; set; }
    public DateTimeOffset CreadaEn { get; set; }

    /// <summary>Notificación pendiente que el proceso de envío toma de inmediato.</summary>
    public static Notificacion Nueva(Cita cita, TipoNotificacion tipo, string destinatario, DateTimeOffset ahora) => new()
    {
        Cita = cita,
        Tipo = tipo,
        Destinatario = destinatario,
        ProximoIntentoEn = ahora,
        CreadaEn = ahora,
    };

    /// <inheritdoc cref="Nueva(Cita, TipoNotificacion, string, DateTimeOffset)"/>
    public static Notificacion Nueva(int citaId, TipoNotificacion tipo, string destinatario, DateTimeOffset ahora) => new()
    {
        CitaId = citaId,
        Tipo = tipo,
        Destinatario = destinatario,
        ProximoIntentoEn = ahora,
        CreadaEn = ahora,
    };

    /// <summary>Espera antes del siguiente intento según los fallos acumulados (docs/10#proceso-de-envío).</summary>
    public static TimeSpan? EsperaTrasFallo(int intentosFallidos) => intentosFallidos switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromHours(1),
        5 => TimeSpan.FromHours(4),
        _ => null,
    };

    public void MarcarEnviada(DateTimeOffset ahora)
    {
        Estado = EstadoNotificacion.Enviada;
        EnviadaEn = ahora;
        UltimoError = null;
    }

    /// <summary>Cuenta el fallo y programa el reintento, o descarta al llegar a <see cref="IntentosMaximos"/>.</summary>
    /// <returns><see langword="true"/> si quedó descartada.</returns>
    public bool RegistrarFallo(string error, DateTimeOffset ahora)
    {
        Intentos++;
        UltimoError = error.Length > 1000 ? error[..1000] : error;
        var espera = EsperaTrasFallo(Intentos);
        if (espera is null)
        {
            Estado = EstadoNotificacion.Descartada;
            return true;
        }
        Estado = EstadoNotificacion.Fallida;
        ProximoIntentoEn = ahora + espera.Value;
        return false;
    }

    /// <summary>Ya no aplica, por ejemplo una confirmación de una cita que se canceló antes del envío.</summary>
    public void Descartar(string motivo)
    {
        Estado = EstadoNotificacion.Descartada;
        UltimoError = motivo;
    }
}
