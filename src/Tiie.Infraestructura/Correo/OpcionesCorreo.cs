using System.ComponentModel.DataAnnotations;

namespace Tiie.Infraestructura.Correo;

public enum ProveedorCorreo
{
    /// <summary>Escribe archivos .eml en <see cref="OpcionesCorreo.Carpeta"/>; solo desarrollo.</summary>
    Archivo,

    /// <summary>Azure Communication Services Email con la identidad administrada del sitio.</summary>
    Acs,
}

/// <summary>Sección <c>Correo</c> de la configuración.</summary>
public sealed class OpcionesCorreo : IValidatableObject
{
    public const string Seccion = "Correo";

    public ProveedorCorreo Proveedor { get; set; } = ProveedorCorreo.Archivo;

    [Required, EmailAddress]
    public string Remitente { get; set; } = "";

    [Required]
    public string NombreRemitente { get; set; } = "TIIE Sinaloa";

    /// <summary>Endpoint de Communication Services, por ejemplo <c>https://acs-tiie-prod.communication.azure.com</c>.</summary>
    public string? AcsEndpoint { get; set; }

    /// <summary>Carpeta de los .eml cuando el proveedor es <see cref="ProveedorCorreo.Archivo"/>.</summary>
    public string Carpeta { get; set; } = "artifacts/correos";

    /// <summary>Solo en QA: si tiene elementos, los correos a otras direcciones no salen.</summary>
    public IList<string> DestinatariosPermitidos { get; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Proveedor == ProveedorCorreo.Acs && !Uri.TryCreate(AcsEndpoint, UriKind.Absolute, out _))
            yield return new ValidationResult("Correo:AcsEndpoint es obligatorio con el proveedor Acs.", [nameof(AcsEndpoint)]);
    }
}
