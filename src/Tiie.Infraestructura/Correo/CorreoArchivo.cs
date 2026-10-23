using System.Globalization;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Privacidad;

namespace Tiie.Infraestructura.Correo;

/// <summary>
/// Escribe cada correo como un archivo .eml, para abrirlo con cualquier cliente de correo. Solo desarrollo.
/// </summary>
public sealed class CorreoArchivo(
    IOptions<OpcionesCorreo> opciones, IHostEnvironment entorno, TimeProvider tiempo, ILogger<CorreoArchivo> logger)
    : ICorreo
{
    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var carpeta = Path.GetFullPath(Path.Combine(entorno.ContentRootPath, "..", "..", opciones.Value.Carpeta));
        Directory.CreateDirectory(carpeta);
        var ahora = tiempo.GetUtcNow();
        var archivo = Path.Combine(carpeta, $"{ahora:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");

        await File.WriteAllTextAsync(archivo, Componer(mensaje, ahora), Encoding.UTF8, cancellationToken);
        logger.LogInformation("Correo escrito en {Archivo} para {Destinatario}", archivo, Enmascarar.Correo(mensaje.Destinatario));
    }

    private string Componer(MensajeCorreo mensaje, DateTimeOffset fecha)
    {
        var limite = $"tiie-{Guid.NewGuid():N}";
        var alternativo = $"alt-{Guid.NewGuid():N}";
        var sb = new StringBuilder();
        sb.Append("From: ").Append(Codificar(opciones.Value.NombreRemitente)).Append(" <").Append(opciones.Value.Remitente).Append(">\r\n");
        sb.Append("To: ").Append(mensaje.Destinatario).Append("\r\n");
        sb.Append("Subject: ").Append(Codificar(mensaje.Asunto)).Append("\r\n");
        sb.Append("Date: ").Append(fecha.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(limite).Append("\"\r\n\r\n");

        sb.Append("--").Append(limite).Append("\r\n");
        sb.Append("Content-Type: multipart/alternative; boundary=\"").Append(alternativo).Append("\"\r\n\r\n");
        Parte(sb, alternativo, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(mensaje.Texto));
        Parte(sb, alternativo, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(mensaje.Html));
        sb.Append("--").Append(alternativo).Append("--\r\n");

        foreach (var adjunto in mensaje.Adjuntos ?? [])
        {
            Parte(sb, limite, $"{adjunto.TipoContenido}; name=\"{adjunto.Nombre}\"", adjunto.Contenido.ToArray(),
                $"Content-Disposition: attachment; filename=\"{adjunto.Nombre}\"\r\n");
        }
        sb.Append("--").Append(limite).Append("--\r\n");
        return sb.ToString();
    }

    private static void Parte(StringBuilder sb, string limite, string tipo, byte[] contenido, string? encabezadoExtra = null)
    {
        sb.Append("--").Append(limite).Append("\r\n");
        sb.Append("Content-Type: ").Append(tipo).Append("\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n");
        if (encabezadoExtra is not null)
            sb.Append(encabezadoExtra);
        sb.Append("\r\n");
        sb.Append(Convert.ToBase64String(contenido, Base64FormattingOptions.InsertLineBreaks)).Append("\r\n\r\n");
    }

    private static string Codificar(string texto) => $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(texto))}?=";
}
