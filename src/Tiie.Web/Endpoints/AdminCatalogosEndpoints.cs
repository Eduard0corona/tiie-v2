using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Aplicacion.Catalogos;

namespace Tiie.Web.Endpoints;

/// <summary>Módulos, tipos de trámite con requisitos y ofertas (RF-12, RF-13).</summary>
internal static class AdminCatalogosEndpoints
{
    public static void Map(RouteGroupBuilder personal, RouteGroupBuilder supervisor, RouteGroupBuilder administrador)
    {
        const string Etiqueta = "Administración · Catálogos";
        var verTodos = personal.MapGroup("").WithTags(Etiqueta);
        var supervisar = supervisor.MapGroup("").WithTags(Etiqueta);
        var administrar = administrador.MapGroup("").WithTags(Etiqueta);

        // Módulos
        verTodos.MapGet("/modulos", async (bool? activo, ModulosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ListarAsync(activo, ct)));
        verTodos.MapGet("/modulos/{moduloId:int}", async (int moduloId, ModulosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ObtenerAsync(moduloId, ct)));
        administrar.MapPost("/modulos", async Task<Created<ModuloDetalle>> (DatosModulo datos, ModulosManejador m, CancellationToken ct) =>
            {
                var id = await m.CrearAsync(datos, ct);
                return TypedResults.Created($"/api/admin/modulos/{id}", await m.ObtenerAsync(id, ct));
            });
        administrar.MapPut("/modulos/{moduloId:int}", async (int moduloId, DatosModulo datos, ModulosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ActualizarAsync(moduloId, datos, ct)));
        administrar.MapPost("/modulos/{moduloId:int}/activacion", async (int moduloId, SolicitudActivo solicitud, ModulosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.CambiarActivoAsync(moduloId, solicitud.Activo, ct)));

        // Tipos de trámite
        verTodos.MapGet("/tipos-tramite", async (TiposTramiteManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ListarAsync(ct)));
        verTodos.MapGet("/tipos-tramite/{tipoTramiteId:int}", async (int tipoTramiteId, TiposTramiteManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ObtenerAsync(tipoTramiteId, ct)));
        administrar.MapPost("/tipos-tramite", async Task<Created<TipoTramiteDetalle>> (DatosTipoTramite datos, TiposTramiteManejador m, CancellationToken ct) =>
            {
                var id = await m.CrearAsync(datos, ct);
                return TypedResults.Created($"/api/admin/tipos-tramite/{id}", await m.ObtenerAsync(id, ct));
            });
        administrar.MapPut("/tipos-tramite/{tipoTramiteId:int}", async (int tipoTramiteId, DatosTipoTramite datos, TiposTramiteManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ActualizarAsync(tipoTramiteId, datos, ct)));

        // Ofertas
        verTodos.MapGet("/ofertas", async (int? moduloId, OfertasManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ListarAsync(moduloId, ct)));
        verTodos.MapGet("/ofertas/{ofertaId:int}", async (int ofertaId, OfertasManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ObtenerAsync(ofertaId, ct)));
        supervisar.MapPost("/ofertas", async Task<Created<OfertaAdmin>> (CrearOferta datos, OfertasManejador m, CancellationToken ct) =>
            {
                var id = await m.CrearAsync(datos, ct);
                return TypedResults.Created($"/api/admin/ofertas/{id}", await m.ObtenerAsync(id, ct));
            });
        supervisar.MapPut("/ofertas/{ofertaId:int}", async (int ofertaId, ActualizarOferta datos, OfertasManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ActualizarAsync(ofertaId, datos, ct)));
    }
}
