namespace Tiie.Web.Infraestructura;

/// <summary>
/// Cabeceras de seguridad en todas las respuestas (docs/11-seguridad-y-privacidad.md#cabeceras).
/// HSTS lo agrega <c>UseHsts</c>.
/// </summary>
public static class CabecerasDeSeguridad
{
    public const string PoliticaContenido =
        "default-src 'self'; " +
        "script-src 'self' https://challenges.cloudflare.com; " +
        "frame-src https://challenges.cloudflare.com; " +
        // Radix posiciona elementos con atributos style; no se permite en scripts.
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "upgrade-insecure-requests";

    public static IApplicationBuilder UseCabecerasDeSeguridad(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            // Se decide con la ruta original: el fallback de la SPA la reescribe a /index.html.
            // El enlace de «Mi cita» lleva el token: nunca se envía como Referer.
            var politicaReferer = contexto.Request.Path.StartsWithSegments("/mi-cita")
                ? "no-referrer"
                : "strict-origin-when-cross-origin";
            contexto.Response.OnStarting(() =>
            {
                var cabeceras = contexto.Response.Headers;
                cabeceras.ContentSecurityPolicy = PoliticaContenido;
                cabeceras.XContentTypeOptions = "nosniff";
                cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                cabeceras["Cross-Origin-Opener-Policy"] = "same-origin";
                cabeceras["Referrer-Policy"] = politicaReferer;
                return Task.CompletedTask;
            });
            await siguiente(contexto);
        });
}
