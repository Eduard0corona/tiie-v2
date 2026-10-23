using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;

namespace Tiie.Infraestructura.Captcha;

/// <summary>
/// Verifica el token de Cloudflare Turnstile en el servidor (docs/11#captcha). Un token se acepta una sola vez.
/// Si Turnstile no responde a tiempo, la solicitud se rechaza: nunca pasa sin verificar.
/// </summary>
public sealed class CaptchaTurnstile(HttpClient http, IOptions<OpcionesCaptcha> opciones, ILogger<CaptchaTurnstile> logger)
    : ICaptcha
{
    public static readonly Uri Verificacion = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);

    public async Task ExigirValidoAsync(string? token, IPAddress? ip, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new CaptchaInvalidoException();

        var campos = new Dictionary<string, string>
        {
            ["secret"] = opciones.Value.ClaveSecreta,
            ["response"] = token,
        };
        if (ip is not null)
            campos["remoteip"] = ip.ToString();

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(Espera);
        try
        {
            using var contenido = new FormUrlEncodedContent(campos);
            using var respuesta = await http.PostAsync(Verificacion, contenido, limite.Token);
            var resultado = await respuesta.Content.ReadFromJsonAsync<RespuestaTurnstile>(limite.Token);
            if (resultado is not { Success: true })
            {
                logger.LogInformation("Captcha rechazado: {Errores}", string.Join(",", resultado?.ErrorCodes ?? []));
                throw new CaptchaInvalidoException();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Turnstile no respondió; la solicitud se rechaza.");
            throw new CaptchaInvalidoException("No se pudo verificar el captcha.", ex);
        }
    }

    private sealed record RespuestaTurnstile(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
