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
}
