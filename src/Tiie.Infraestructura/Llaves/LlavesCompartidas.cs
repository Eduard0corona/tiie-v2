using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tiie.Infraestructura.Llaves;

/// <summary>Sección <c>DataProtection</c>: llaves compartidas entre instancias, ranuras y herramientas.</summary>
public sealed class OpcionesDataProtection
{
    public const string Seccion = "DataProtection";

    /// <summary>Blob donde se guardan las llaves, por ejemplo <c>https://sttiieprod.blob.core.windows.net/dataprotection/llaves.xml</c>.</summary>
    public string? BlobUri { get; set; }

    /// <summary>Llave de Key Vault que cifra las llaves.</summary>
    public string? KeyUri { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(BlobUri) && !string.IsNullOrWhiteSpace(KeyUri);
}

public static class LlavesCompartidas
{
    /// <summary>Nombre de aplicación común: el sitio y tools/Tiie.Migracion leen lo que protege el otro.</summary>
    public const string NombreAplicacion = "Tiie";

    /// <summary>
    /// Data Protection con las llaves de Storage cifradas con Key Vault cuando la sección está configurada
    /// (docs/07#data-protection); si no, las llaves quedan en el perfil local del usuario.
    /// </summary>
    public static IDataProtectionBuilder AgregarLlavesCompartidas(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName(NombreAplicacion);
        var opciones = configuration.GetSection(OpcionesDataProtection.Seccion).Get<OpcionesDataProtection>() ?? new();
        if (opciones.Configurado)
        {
            // En Azure, la identidad administrada del sitio; en una herramienta, la sesión de az del operador.
            var credencial = new DefaultAzureCredential();
            dataProtection
                .PersistKeysToAzureBlobStorage(new Uri(opciones.BlobUri!), credencial)
                .ProtectKeysWithAzureKeyVault(new Uri(opciones.KeyUri!), credencial);
        }
        return dataProtection;
    }
}
