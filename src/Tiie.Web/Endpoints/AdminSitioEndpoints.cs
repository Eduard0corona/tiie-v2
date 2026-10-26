using Tiie.Aplicacion.Sitio;

namespace Tiie.Web.Endpoints;

/// <summary>Contenido del sitio público que edita el administrador: la portada.</summary>
internal static class AdminSitioEndpoints
{
    public static void Map(RouteGroupBuilder administrador)
    {
        var administrar = administrador.MapGroup("").WithTags("Administración · Sitio");

        administrar.MapGet("/portada", async (PortadaManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ObtenerAsync(ct)));
        administrar.MapPut("/portada", async (DatosPortada datos, PortadaManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.GuardarAsync(datos, ct)));
    }
}
