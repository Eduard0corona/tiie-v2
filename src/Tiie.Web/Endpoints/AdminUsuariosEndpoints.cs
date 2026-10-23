using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Dominio.Errores;
using Tiie.Infraestructura.Identidad;
using Tiie.Web.Errores;

namespace Tiie.Web.Endpoints;

/// <summary>Usuarios del personal (RF-16). Solo Administrador.</summary>
internal static class AdminUsuariosEndpoints
{
    public static void Map(RouteGroupBuilder administrador)
    {
        var g = administrador.MapGroup("").WithTags("Administración · Usuarios");

        g.MapGet("/usuarios", async (string? busqueda, string? rol, bool? activo, int? pagina, int? tamano, AdministracionUsuarios m, CancellationToken ct) =>
            TypedResults.Ok(await m.ListarAsync(busqueda, rol, activo, pagina, tamano, ct)));

        g.MapPost("/usuarios", async Task<Results<Created<UsuarioDetalle>, ProblemHttpResult>> (CrearUsuario datos, AdministracionUsuarios m, CancellationToken ct) =>
            {
                var creado = await m.CrearAsync(datos, ct);
                // La cuenta existe aunque el correo falle: el 503 lleva su id para reenviar la invitación.
                if (!creado.InvitacionEnviada)
                    return ErroresApi.Problema(CodigoError.CorreoNoDisponible, AdministracionUsuarios.MensajeInvitacionNoEnviada,
                        [new("usuarioId", creado.UsuarioId)]);
                return TypedResults.Created($"/api/admin/usuarios/{creado.UsuarioId}", await m.ObtenerAsync(creado.UsuarioId, ct));
            });

        g.MapGet("/usuarios/{usuarioId:int}", async (int usuarioId, AdministracionUsuarios m, CancellationToken ct) =>
            TypedResults.Ok(await m.ObtenerAsync(usuarioId, ct)));

        g.MapPut("/usuarios/{usuarioId:int}", async (int usuarioId, ActualizarUsuario datos, AdministracionUsuarios m, CancellationToken ct) =>
            TypedResults.Ok(await m.ActualizarAsync(usuarioId, datos, ct)));

        g.MapPost("/usuarios/{usuarioId:int}/activacion", async (int usuarioId, SolicitudActivo solicitud, AdministracionUsuarios m, CancellationToken ct) =>
            TypedResults.Ok(await m.CambiarActivoAsync(usuarioId, solicitud.Activo, ct)));

        g.MapPost("/usuarios/{usuarioId:int}/invitacion", async Task<Accepted> (int usuarioId, AdministracionUsuarios m, CancellationToken ct) =>
            {
                await m.ReenviarInvitacionAsync(usuarioId, ct);
                return TypedResults.Accepted((string?)null);
            });

        g.MapPost("/usuarios/{usuarioId:int}/mfa/restablecimiento", async Task<NoContent> (int usuarioId, AdministracionUsuarios m, CancellationToken ct) =>
            {
                await m.RestablecerMfaAsync(usuarioId, ct);
                return TypedResults.NoContent();
            });
    }
}
