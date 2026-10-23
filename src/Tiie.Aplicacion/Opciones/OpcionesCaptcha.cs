using System.ComponentModel.DataAnnotations;

namespace Tiie.Aplicacion.Opciones;

/// <summary>Sección <c>Captcha</c>: Cloudflare Turnstile. La clave secreta llega de Key Vault.</summary>
public sealed class OpcionesCaptcha
{
    public const string Seccion = "Captcha";

    [Required]
    public string ClaveSitio { get; set; } = "";

    [Required]
    public string ClaveSecreta { get; set; } = "";
}
