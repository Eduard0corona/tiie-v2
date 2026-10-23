using Tiie.Dominio.Notificaciones;

namespace Tiie.Aplicacion.Puertos;

/// <summary>
/// Arma los correos a partir de las plantillas de <c>Tiie.Infraestructura/Correo/Plantillas</c>.
/// Cada valor se codifica para HTML antes de insertarlo.
/// </summary>
public interface IPlantillasCorreo
{
    /// <summary>Invitación a una cuenta del personal, con el enlace para definir la contraseña.</summary>
    MensajeCorreo Invitacion(string destinatario, string nombre, string enlace);

    /// <summary>Restablecimiento de contraseña del personal.</summary>
    MensajeCorreo Restablecimiento(string destinatario, string nombre, string enlace);

    /// <summary>Correo al ciudadano de la bandeja de salida (docs/10#tipos-de-notificación).</summary>
    MensajeCorreo Notificacion(TipoNotificacion tipo, string destinatario, DatosNotificacion datos);
}

/// <summary>Valores de los marcadores de las plantillas de notificación, ya formateados.</summary>
public sealed record DatosNotificacion(
    string Nombre,
    string Folio,
    string Fecha,
    string Hora,
    string Modulo,
    string Direccion,
    string Tramite,
    IReadOnlyList<string> Requisitos,
    string Enlace,
    string? Motivo,
    string AvisoPrivacidad);
