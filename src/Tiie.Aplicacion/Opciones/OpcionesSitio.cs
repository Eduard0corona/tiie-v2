using System.ComponentModel.DataAnnotations;

namespace Tiie.Aplicacion.Opciones;

/// <summary>Sección <c>Sitio</c> de la configuración.</summary>
public sealed class OpcionesSitio
{
    public const string Seccion = "Sitio";

    /// <summary>Origen público, sin diagonal final. Arma los enlaces de los correos y la verificación de Origin.</summary>
    [Required, Url]
    public string UrlPublica { get; set; } = "";

    /// <summary>Versión vigente del aviso de privacidad; se guarda en cada cita.</summary>
    [Required, MaxLength(20)]
    public string VersionAvisoPrivacidad { get; set; } = "";

    /// <summary>URL pública sin diagonal final, para concatenar rutas.</summary>
    public string UrlBase => UrlPublica.TrimEnd('/');
}
