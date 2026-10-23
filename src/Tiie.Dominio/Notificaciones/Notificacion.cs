using Tiie.Dominio.Citas;

namespace Tiie.Dominio.Notificaciones;

/// <summary>Mensaje en la bandeja de salida. Se inserta en la transacción de la cita (ADR 0007).</summary>
public sealed class Notificacion
{
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
}
