using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;

namespace Tiie.Aplicacion.Bitacora;

/// <summary>Última ejecución de un proceso programado.</summary>
public sealed record EstadoProceso(
    string Nombre,
    DateTimeOffset? UltimoInicio,
    DateTimeOffset? UltimoFin,
    string? UltimoResultado,
    string? Detalle);

public sealed class ConsultarProcesosManejador(ITiieDb db)
{
    public async Task<IReadOnlyList<EstadoProceso>> EjecutarAsync(CancellationToken ct) =>
        await db.EjecucionesProceso.AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new EstadoProceso(e.Nombre, e.UltimoInicio, e.UltimoFin, e.UltimoResultado, e.Detalle))
            .ToListAsync(ct);
}
