using Microsoft.AspNetCore.StaticFiles;

namespace Tiie.Web.Infraestructura;

/// <summary>Archivos de la SPA que compila Vite en <c>wwwroot</c>.</summary>
public static class Spa
{
    /// <summary>
    /// <c>index.html</c> nunca se guarda en caché: así cada despliegue llega de inmediato. Los archivos de
    /// <c>assets/</c> llevan huella en el nombre y los sirve <c>MapStaticAssets</c> con caché de un año.
    /// </summary>
    public static StaticFileOptions SinCache { get; } = new()
    {
        ContentTypeProvider = new FileExtensionContentTypeProvider(),
        OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache",
    };
}
