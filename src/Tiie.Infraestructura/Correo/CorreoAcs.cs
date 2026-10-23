using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Privacidad;

namespace Tiie.Infraestructura.Correo;

/// <summary>
/// Envío con Azure Communication Services Email, autenticado con la identidad administrada del sitio.
/// Devuelve en cuanto el servicio acepta el mensaje; no espera la entrega.
/// </summary>
public sealed class CorreoAcs(EmailClient cliente, IOptions<OpcionesCorreo> opciones, ILogger<CorreoAcs> logger) : ICorreo
{
    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var contenido = new EmailContent(mensaje.Asunto) { Html = mensaje.Html, PlainText = mensaje.Texto };
        var correo = new EmailMessage(opciones.Value.Remitente, mensaje.Destinatario, contenido);
        foreach (var adjunto in mensaje.Adjuntos ?? [])
            correo.Attachments.Add(new EmailAttachment(adjunto.Nombre, adjunto.TipoContenido, BinaryData.FromBytes(adjunto.Contenido)));

        try
        {
            var operacion = await cliente.SendAsync(WaitUntil.Started, correo, cancellationToken);
            logger.LogInformation("Correo aceptado por Communication Services ({OperacionId}) para {Destinatario}",
                operacion.Id, Enmascarar.Correo(mensaje.Destinatario));
        }
        catch (RequestFailedException ex)
        {
            // El mensaje del proveedor puede incluir el destinatario: solo se conserva el código.
            throw new CorreoNoEnviadoException($"Communication Services rechazó el correo ({ex.Status} {ex.ErrorCode}).", ex);
        }
    }
}
