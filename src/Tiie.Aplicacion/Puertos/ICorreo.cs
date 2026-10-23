namespace Tiie.Aplicacion.Puertos;

/// <summary>Archivo adjunto de un correo, por ejemplo el comprobante PDF.</summary>
public sealed record AdjuntoCorreo(string Nombre, string TipoContenido, ReadOnlyMemory<byte> Contenido);

/// <summary>Correo listo para enviar: un destinatario, versión HTML y versión de texto.</summary>
public sealed record MensajeCorreo(
    string Destinatario,
    string Asunto,
    string Html,
    string Texto,
    IReadOnlyList<AdjuntoCorreo>? Adjuntos = null);

/// <summary>Envío de correo. Producción: Communication Services; desarrollo: archivos .eml.</summary>
public interface ICorreo
{
    /// <exception cref="CorreoNoEnviadoException">El proveedor rechazó el mensaje o no respondió.</exception>
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken);
}

/// <summary>El proveedor no aceptó el correo. El mensaje no contiene datos personales.</summary>
public sealed class CorreoNoEnviadoException : Exception
{
    public CorreoNoEnviadoException() { }

    public CorreoNoEnviadoException(string message)
        : base(message) { }

    public CorreoNoEnviadoException(string message, Exception innerException)
        : base(message, innerException) { }
}
