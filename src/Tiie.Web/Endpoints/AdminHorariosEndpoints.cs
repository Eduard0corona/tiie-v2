using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Aplicacion.Horarios;

namespace Tiie.Web.Endpoints;

/// <summary>Plantillas, ajuste de cupos y bloqueos (RF-14, RF-15).</summary>
internal static class AdminHorariosEndpoints
{
    public static void Map(RouteGroupBuilder personal, RouteGroupBuilder supervisor)
    {
        const string Etiqueta = "Administración · Horarios";
        var ver = personal.MapGroup("").WithTags(Etiqueta);
        var editar = supervisor.MapGroup("").WithTags(Etiqueta);

        ver.MapGet("/ofertas/{ofertaId:int}/plantilla", async (int ofertaId, PlantillasManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ObtenerAsync(ofertaId, ct)));
        editar.MapPut("/ofertas/{ofertaId:int}/plantilla", async (int ofertaId, PlantillaSemanal plantilla, PlantillasManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.GuardarAsync(ofertaId, plantilla, ct)));

        editar.MapPatch("/cupos/{cupoId:int}", async (int cupoId, AjusteCupo ajuste, CuposManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.AjustarAsync(cupoId, ajuste, ct)));

        ver.MapGet("/bloqueos", async (DateOnly? desde, DateOnly? hasta, int? moduloId, BloqueosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ListarAsync(desde, hasta, moduloId, ct)));
        // Un bloqueo general (día inhábil) además exige rol Administrador; lo revisa el caso de uso.
        editar.MapPost("/bloqueos", async Task<Created<BloqueoCreado>> (CrearBloqueo datos, BloqueosManejador m, CancellationToken ct) =>
            {
                var creado = await m.CrearAsync(datos, ct);
                return TypedResults.Created($"/api/admin/bloqueos/{creado.BloqueoId}", creado);
            });
        editar.MapPost("/bloqueos/{bloqueoId:int}/cancelacion-citas", async (int bloqueoId, SolicitudMotivo solicitud, BloqueosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.CancelarCitasAsync(bloqueoId, solicitud.Motivo, ct)));
        editar.MapDelete("/bloqueos/{bloqueoId:int}", async Task<NoContent> (int bloqueoId, BloqueosManejador m, CancellationToken ct) =>
            {
                await m.EliminarAsync(bloqueoId, ct);
                return TypedResults.NoContent();
            });
    }
}
