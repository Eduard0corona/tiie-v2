using System.Net;

namespace Tiie.Aplicacion.Puertos;

/// <summary>Verificación del captcha (Cloudflare Turnstile) en la reserva y la recuperación del enlace.</summary>
public interface ICaptcha
{
    /// <exception cref="CaptchaInvalidoException">El token no es válido, ya se usó o el servicio no respondió.</exception>
    Task ExigirValidoAsync(string? token, IPAddress? ip, CancellationToken cancellationToken);
}

/// <summary>El captcha no pasó; se responde 422 <c>CaptchaInvalido</c>.</summary>
public sealed class CaptchaInvalidoException : Exception
{
    public CaptchaInvalidoException()
        : base("El captcha no es válido.") { }

    public CaptchaInvalidoException(string message)
        : base(message) { }

    public CaptchaInvalidoException(string message, Exception innerException)
        : base(message, innerException) { }
}
