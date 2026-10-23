using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Privacidad;

namespace Tiie.Infraestructura.Correo;

/// <summary>
/// En QA solo salen los correos a <c>Correo:DestinatariosPermitidos</c>; los demás se dan por enviados sin
/// salir (docs/10#envío). Sin lista configurada, todo pasa.
/// </summary>
public sealed class CorreoConListaPermitida(ICorreo interno, IOptions<OpcionesCorreo> opciones, ILogger<CorreoConListaPermitida> logger)
    : ICorreo
{
    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var permitidos = opciones.Value.DestinatariosPermitidos;
        if (permitidos.Count > 0 && !permitidos.Contains(mensaje.Destinatario, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogInformation("Correo a {Destinatario} omitido: no está en la lista de destinatarios permitidos",
                Enmascarar.Correo(mensaje.Destinatario));
            return Task.CompletedTask;
        }
        return interno.EnviarAsync(mensaje, cancellationToken);
    }
}
