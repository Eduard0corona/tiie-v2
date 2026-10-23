using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Aplicacion.MiCita;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Web.Endpoints;

public sealed record SolicitudCancelacionMiCita(string? Motivo);

public sealed record SolicitudReprogramacionMiCita(int CupoId);

/// <summary><c>/mi-cita/{token}</c> y <c>/api/mi-cita</c>: el ciudadano accede con el token de su correo (RN-16).</summary>
public static class MiCitaEndpoints
{
    public static void MapEndpointsMiCita(this IEndpointRouteBuilder app)
    {
        // El token viaja una sola vez: se cambia por la cookie y se redirige para que no quede en la barra.
        app.MapGet("/mi-cita/{token}", async Task<RedirectHttpResult> (string token, AccederConTokenManejador m,
                HttpContext http, CancellationToken ct) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                var acceso = await m.EjecutarAsync(token, ct);
                if (acceso is null)
                    return TypedResults.Redirect("/mi-cita?error=enlace-invalido");
                await AccesoCita.IniciarSesionAsync(http, acceso);
                return TypedResults.Redirect("/mi-cita");
            })
            .AllowAnonymous()
            .RequireRateLimiting(Limites.EnlaceCita)
            .ExcludeFromDescription();

        var g = app.MapGroup("/api/mi-cita")
            .WithTags("Mi cita")
            .RequireAuthorization(Politicas.Cita)
            .AddEndpointFilter<FiltroCsrf>()
            .RequireRateLimiting(Limites.MiCita);

        g.MapGet("", async (ClaimsPrincipal principal, MiCitaManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ConsultarAsync(AccesoCita.CitaId(principal), ct)));

        g.MapGet("/comprobante.pdf", async (ClaimsPrincipal principal, MiCitaManejador m, CancellationToken ct) =>
            {
                var (contenido, folio) = await m.ComprobanteAsync(AccesoCita.CitaId(principal), ct);
                return TypedResults.File(contenido, "application/pdf", $"comprobante-{folio}.pdf");
            });

        g.MapPost("/cancelacion", async (SolicitudCancelacionMiCita solicitud, ClaimsPrincipal principal,
                MiCitaManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.CancelarAsync(AccesoCita.CitaId(principal), solicitud.Motivo, ct)));

        g.MapPost("/reprogramacion", async (SolicitudReprogramacionMiCita solicitud, ClaimsPrincipal principal,
                MiCitaManejador m, HttpContext http, CancellationToken ct) =>
            {
                var (detalle, acceso) = await m.ReprogramarAsync(AccesoCita.CitaId(principal), solicitud.CupoId, ct);
                // La cookie pasa a la cita nueva; el enlace anterior sigue sirviendo solo para consultar la original.
                await AccesoCita.IniciarSesionAsync(http, acceso);
                return TypedResults.Ok(detalle);
            });

        g.MapPost("/salida", async (HttpContext http) =>
            {
                await AccesoCita.CerrarSesionAsync(http);
                return TypedResults.NoContent();
            });
    }
}
