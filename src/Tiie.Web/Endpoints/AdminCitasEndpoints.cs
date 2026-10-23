using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Horarios;
using Tiie.Dominio.Citas;

namespace Tiie.Web.Endpoints;

public sealed record SolicitudAsistencia(bool Asistio);

public sealed record SolicitudCorreccionAsistencia(bool Asistio, string? Motivo);

public sealed record SolicitudReprogramacionPersonal(int CupoId, string? Motivo);

/// <summary>Agenda del día, citas y cupos del panel (RF-09, RF-10, RF-11).</summary>
internal static class AdminCitasEndpoints
{
    public static void Map(RouteGroupBuilder personal, RouteGroupBuilder supervisor)
    {
        var citas = personal.MapGroup("").WithTags("Administración · Citas");

        citas.MapGet("/agenda", async (int moduloId, DateOnly? fecha, CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.AgendaAsync(moduloId, fecha, ct)));

        citas.MapGet("/citas", async (DateOnly? desde, DateOnly? hasta, int? moduloId, int? tipoTramiteId, EstadoCita? estado,
                string? busqueda, int? pagina, int? tamano, CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.BuscarAsync(new FiltroCitas(desde, hasta, moduloId, tipoTramiteId, estado, busqueda, pagina, tamano), ct)));

        citas.MapGet("/citas/{citaId:int}", async (int citaId, CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.DetalleAsync(citaId, ct)));

        citas.MapPost("/citas", async Task<Created<CitaRegistrada>> (CitaVentanilla solicitud,
                [FromHeader(Name = "Idempotency-Key")] Guid clave, CitasPersonalManejador m, CancellationToken ct) =>
            {
                var registrada = await m.VentanillaAsync(solicitud, clave, ct);
                return TypedResults.Created($"/api/admin/citas/{registrada.CitaId}", registrada);
            });

        citas.MapPost("/citas/{citaId:int}/asistencia", async (int citaId, SolicitudAsistencia solicitud, CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.RegistrarAsistenciaAsync(citaId, solicitud.Asistio, ct)));

        citas.MapPost("/citas/{citaId:int}/cancelacion", async (int citaId, SolicitudMotivo solicitud, CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.CancelarAsync(citaId, solicitud.Motivo, ct)));

        citas.MapPost("/citas/{citaId:int}/reprogramacion", async (int citaId, SolicitudReprogramacionPersonal solicitud,
                CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ReprogramarAsync(citaId, solicitud.CupoId, solicitud.Motivo, ct)));

        citas.MapPost("/citas/{citaId:int}/reenvio", async Task<Accepted> (int citaId, CitasPersonalManejador m, CancellationToken ct) =>
            {
                await m.ReenviarAsync(citaId, ct);
                return TypedResults.Accepted((string?)null);
            });

        citas.MapGet("/citas/{citaId:int}/comprobante.pdf", async (int citaId, CitasPersonalManejador m, CancellationToken ct) =>
            {
                var (contenido, folio) = await m.ComprobanteAsync(citaId, ct);
                return TypedResults.File(contenido, "application/pdf", $"comprobante-{folio}.pdf");
            });

        citas.MapGet("/ofertas/{ofertaId:int}/cupos", async (int ofertaId, DateOnly? desde, DateOnly? hasta, CuposManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ListarAsync(ofertaId, desde, hasta, ct)));

        supervisor.MapPost("/citas/{citaId:int}/correccion-asistencia", async (int citaId, SolicitudCorreccionAsistencia solicitud,
                CitasPersonalManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.CorregirAsistenciaAsync(citaId, solicitud.Asistio, solicitud.Motivo, ct)))
            .WithTags("Administración · Citas");
    }
}
