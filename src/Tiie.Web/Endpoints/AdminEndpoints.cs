using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Catalogos;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Web.Endpoints;

/// <summary>
/// <c>/api/admin</c>: se divide en subgrupos por política. Una prueba recorre los endpoints y falla si alguno
/// no tiene metadatos de autorización.
/// </summary>
public static class AdminEndpoints
{
    public static void MapEndpointsAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .AddEndpointFilter<FiltroCsrf>()
            .RequireRateLimiting(Limites.Admin);

        admin.MapAdminAuth();

        var personal = admin.MapGroup("").RequireAuthorization(Politicas.Personal).WithTags("Administración · Catálogos");
        personal.MapGet("/catalogos", async (ConsultarCatalogosAdminManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(ct)));

        var administrador = admin.MapGroup("").RequireAuthorization(Politicas.Administrador).WithTags("Administración · Auditoría");
        administrador.MapGet("/procesos", async Task<Ok<IReadOnlyList<EstadoProceso>>> (ConsultarProcesosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(ct)));
    }
}
