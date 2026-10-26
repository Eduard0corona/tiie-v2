using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Tiie.Aplicacion.MiCita;
using Tiie.Aplicacion.Publico;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reservas;
using Tiie.Aplicacion.Sitio;
using Tiie.Dominio.Citas;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Web.Endpoints;

/// <summary>Reserva desde el sitio público. La CURP del ejemplo de docs/06 es ficticia.</summary>
public sealed record SolicitudCita(
    int CupoId,
    string Curp,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string? Celular,
    string? Correo,
    bool AceptaAvisoPrivacidad,
    string? TokenCaptcha)
{
    public CrearCita ACrearCita(Guid clave) => new(
        CupoId, Curp ?? "", Nombre ?? "", ApellidoPaterno ?? "", ApellidoMaterno, Celular, Correo,
        AceptaAvisoPrivacidad, clave, OrigenCita.Web, UsuarioId: null);
}

public sealed record SolicitudRecuperacion(string Curp, string Correo, string? TokenCaptcha);

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

        g.MapGet("/portada", async (PortadaManejador m, CancellationToken ct) => TypedResults.Ok(await m.PublicaAsync(ct)));

        g.MapGet("/tipos-tramite", async (ConsultarCatalogoPublicoManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.TiposTramiteAsync(ct)));

        g.MapGet("/municipios", async (ConsultarCatalogoPublicoManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.MunicipiosAsync(ct)));

        g.MapGet("/modulos", async (ConsultarCatalogoPublicoManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ModulosAsync(null, ct)));

        g.MapGet("/municipios/{municipioId:int}/modulos", async (short municipioId, ConsultarCatalogoPublicoManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.ModulosAsync(municipioId, ct)));

        g.MapGet("/modulos/{moduloId:int}/ofertas", async (int moduloId, ConsultarCatalogoPublicoManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.OfertasAsync(moduloId, ct)));

        g.MapGet("/ofertas/{ofertaId:int}/disponibilidad", async (int ofertaId, DateOnly? desde, DateOnly? hasta,
                ConsultarDisponibilidadManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(ofertaId, desde, hasta, ct)));

        g.MapGet("/ofertas/{ofertaId:int}/cupos", async (int ofertaId, DateOnly fecha, ConsultarCuposManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(ofertaId, fecha, ct)));

        g.MapPost("/citas", async Task<Created<CitaReservada>> (SolicitudCita solicitud,
                [FromHeader(Name = "Idempotency-Key")] Guid clave, ICaptcha captcha, CrearCitaManejador m,
                HttpContext http, CancellationToken ct) =>
            {
                await captcha.ExigirValidoAsync(solicitud.TokenCaptcha, http.Connection.RemoteIpAddress, ct);
                var creada = await m.EjecutarAsync(solicitud.ACrearCita(clave), ct);
                // Mi cita se muestra de inmediato, sin esperar el correo.
                await AccesoCita.IniciarSesionAsync(http, new AccesoMiCita(creada.CitaId, creada.TokenVenceEn));
                return TypedResults.Created("/api/mi-cita", creada.Resumen);
            })
            .RequireRateLimiting(Limites.Reserva);

        g.MapPost("/citas/recuperacion", async Task<Accepted> (SolicitudRecuperacion solicitud, ICaptcha captcha,
                RecuperarEnlaceManejador m, HttpContext http, CancellationToken ct) =>
            {
                await captcha.ExigirValidoAsync(solicitud.TokenCaptcha, http.Connection.RemoteIpAddress, ct);
                await m.EjecutarAsync(new RecuperarEnlace(solicitud.Curp ?? "", solicitud.Correo ?? ""), ct);
                return TypedResults.Accepted((string?)null);
            })
            .RequireRateLimiting(Limites.Recuperacion);
    }
}
