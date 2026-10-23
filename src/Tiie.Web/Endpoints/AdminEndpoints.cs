using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Catalogos;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Web.Endpoints;

public sealed record SolicitudActivo(bool Activo);

public sealed record SolicitudMotivo(string? Motivo);

/// <summary>
/// <c>/api/admin</c>: se divide en subgrupos por política. Una prueba recorre los endpoints y falla si alguno
/// no tiene metadatos de autorización o no está en la matriz de permisos.
/// </summary>
public static class AdminEndpoints
{
    public static void MapEndpointsAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .AddEndpointFilter<FiltroCsrf>()
            .RequireRateLimiting(Limites.Admin);

        admin.MapAdminAuth();

        var personal = admin.MapGroup("").RequireAuthorization(Politicas.Personal);
        var supervisor = admin.MapGroup("").RequireAuthorization(Politicas.Supervisor);
        var administrador = admin.MapGroup("").RequireAuthorization(Politicas.Administrador);

        personal.MapGet("/catalogos", async (ConsultarCatalogosAdminManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(ct))).WithTags("Administración · Catálogos");

        AdminCitasEndpoints.Map(personal, supervisor);
        AdminCatalogosEndpoints.Map(personal, supervisor, administrador);
        AdminHorariosEndpoints.Map(personal, supervisor);
        AdminUsuariosEndpoints.Map(administrador);
        AdminInformacionEndpoints.Map(personal, supervisor, administrador);
    }
}

/// <summary>Reportes, tablero, bitácora y procesos (RF-17 a RF-19).</summary>
internal static class AdminInformacionEndpoints
{
    public static void Map(RouteGroupBuilder personal, RouteGroupBuilder supervisor, RouteGroupBuilder administrador)
    {
        supervisor.MapGet("/reportes/citas", async Task<Results<Ok<Aplicacion.Reportes.ReporteCitas>, FileContentHttpResult, ProblemHttpResult>> (
                DateOnly desde, DateOnly hasta, int? moduloId, int? tipoTramiteId, Dominio.Citas.EstadoCita? estado, string? formato,
                Aplicacion.Reportes.ReportesManejador m, CancellationToken ct) =>
            {
                var filtro = new Aplicacion.Reportes.FiltroReporte(desde, hasta, moduloId, tipoTramiteId, estado);
                switch (formato?.ToUpperInvariant())
                {
                    case null or "" or "JSON":
                        return TypedResults.Ok(await m.CitasAsync(filtro, ct));
                    case "XLSX" or "PDF":
                        var archivo = await m.ExportarAsync(filtro,
                            formato.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? Aplicacion.Reportes.FormatoReporte.Pdf : Aplicacion.Reportes.FormatoReporte.Xlsx, ct);
                        return TypedResults.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre);
                    default:
                        return Errores.ErroresApi.Validacion(new Dictionary<string, string[]> { ["formato"] = ["Usa json, xlsx o pdf."] });
                }
            })
            .WithTags("Administración · Reportes");

        personal.MapGet("/tablero", async (int? moduloId, Aplicacion.Reportes.TableroManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(moduloId, ct))).WithTags("Administración · Reportes");

        administrador.MapGet("/auditoria", async (DateOnly? desde, DateOnly? hasta, int? usuarioId, string? accion, string? entidad,
                int? pagina, int? tamano, ConsultarAuditoriaManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(new FiltroAuditoria(desde, hasta, usuarioId, accion, entidad, pagina, tamano), ct)))
            .WithTags("Administración · Auditoría");

        administrador.MapGet("/procesos", async Task<Ok<IReadOnlyList<EstadoProceso>>> (ConsultarProcesosManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(ct))).WithTags("Administración · Auditoría");
    }
}
