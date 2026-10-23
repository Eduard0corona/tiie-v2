using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;
using Tiie.Web.Errores;

namespace Tiie.Web.Seguridad;

/// <summary>
/// Protección CSRF para los tres grupos de rutas: cabecera obligatoria <c>X-Tiie-Csrf: 1</c> y Fetch
/// Metadata. Se usa en lugar de <c>IAntiforgery</c> porque conviven dos esquemas de cookies (ADR 0003).
/// </summary>
public sealed class FiltroCsrf(IOptions<OpcionesSitio> sitio) : IEndpointFilter
{
    public const string Cabecera = "X-Tiie-Csrf";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var req = context.HttpContext.Request;
        if (HttpMethods.IsGet(req.Method) || HttpMethods.IsHead(req.Method) || HttpMethods.IsOptions(req.Method))
            return await next(context);

        if (req.Headers[Cabecera] != "1")
            return ErroresApi.SolicitudInvalida($"Falta la cabecera {Cabecera}.");

        var sitioOrigen = req.Headers["Sec-Fetch-Site"].ToString();
        if (sitioOrigen is not ("" or "same-origin" or "none"))
            return ErroresApi.Prohibido();

        if (sitioOrigen.Length == 0 && req.Headers.Origin is { Count: > 0 } origen
            && !string.Equals(origen, sitio.Value.UrlBase, StringComparison.OrdinalIgnoreCase))
            return ErroresApi.Prohibido();

        return await next(context);
    }
}
