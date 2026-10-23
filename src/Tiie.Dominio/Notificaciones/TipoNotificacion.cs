namespace Tiie.Dominio.Notificaciones;

public enum TipoNotificacion : byte
{
    Confirmacion = 1,
    Recordatorio = 2,
    Cancelacion = 3,
    Reprogramacion = 4,
    EnlaceRecuperacion = 5,
    CierrePorBloqueo = 6,
    AvisoMigracion = 7,
}

public enum CanalNotificacion : byte
{
    Correo = 1,
}

public enum EstadoNotificacion : byte
{
    Pendiente = 1,
    Enviada = 2,
    /// <summary>Falló y se reintentará.</summary>
    Fallida = 3,
    Descartada = 4,
}
