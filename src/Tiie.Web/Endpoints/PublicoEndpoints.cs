using Tiie.Aplicacion.Publico;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Web.Endpoints;

/// <summary><c>/api/publico</c>: anónimo, con límites por IP.</summary>
public static class PublicoEndpoints
{
    public static void MapEndpointsPublicos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/publico")
            .WithTags("Público")
            .AllowAnonymous()
            .AddEndpointFilter<FiltroCsrf>()
            .RequireRateLimiting(Limites.PublicoLectura);

        g.MapGet("/configuracion", (ConsultarConfiguracionManejador m) => TypedResults.Ok(m.Ejecutar()));
    }
}
