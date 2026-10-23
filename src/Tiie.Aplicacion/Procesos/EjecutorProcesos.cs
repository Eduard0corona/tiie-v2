using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Bitacora;

namespace Tiie.Aplicacion.Procesos;

/// <summary>Proceso que corre una vez al día a una hora local (docs/10#procesos-programados).</summary>
public interface IProcesoDiario
{
    string Nombre { get; }

    /// <summary>Hora local a partir de la cual corre ese día.</summary>
    TimeOnly Hora { get; }

    /// <summary>Si tiene valor, solo corre ese día de la semana.</summary>
    DayOfWeek? Dia { get; }

    /// <returns>Resumen sin datos personales para <c>EjecucionProceso.Detalle</c>.</returns>
    Task<string> EjecutarAsync(CancellationToken ct);
}

/// <summary>
/// Corre un proceso con un candado de aplicación de SQL Server y registra su ejecución en <c>EjecucionProceso</c>.
/// Con varias instancias, la que no obtiene el candado no hace nada (ADR 0007).
/// </summary>
public sealed class EjecutorProcesos(ITiieDb db, TimeProvider tiempo, Metricas metricas, ILogger<EjecutorProcesos> logger)
{
    /// <param name="yaCorrioDesde">Si la última ejecución empezó en este instante o después, no se repite.</param>
    /// <returns><see langword="true"/> si esta instancia ejecutó el proceso.</returns>
    public async Task<bool> EjecutarAsync(IProcesoDiario proceso, DateTimeOffset? yaCorrioDesde, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        var recurso = $"tiie:proceso:{proceso.Nombre}";
        var adquirido = false;
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var resultado = (await db.Database.SqlQuery<int>(
                    $"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {recurso}, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r AS Value")
                .ToListAsync(ct)).Single();
            adquirido = resultado >= 0;
            if (!adquirido)
                return false;                                   // otra instancia lo está ejecutando

            var ultimoInicio = await db.EjecucionesProceso.AsNoTracking()
                .Where(e => e.Nombre == proceso.Nombre).Select(e => e.UltimoInicio).SingleOrDefaultAsync(ct);
            if (yaCorrioDesde is { } limite && ultimoInicio >= limite)
                return false;

            await MarcarInicioAsync(proceso.Nombre, ct);
            var cronometro = Stopwatch.StartNew();
            string resultadoFinal;
            string? detalle;
            try
            {
                detalle = await proceso.EjecutarAsync(ct);
                resultadoFinal = EjecucionProceso.Correcto;
                logger.LogInformation("Proceso {Proceso} terminado: {Detalle}", proceso.Nombre, detalle);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resultadoFinal = EjecucionProceso.Error;
                detalle = $"Falló con {ex.GetType().Name}. Revisa los logs.";
                logger.LogError(ex, "Proceso {Proceso} falló", proceso.Nombre);
            }

            var fin = tiempo.GetUtcNow();
            db.ChangeTracker.Clear();
            await db.EjecucionesProceso.Where(e => e.Nombre == proceso.Nombre).ExecuteUpdateAsync(s => s
                .SetProperty(e => e.UltimoFin, fin)
                .SetProperty(e => e.UltimoResultado, resultadoFinal)
                .SetProperty(e => e.Detalle, detalle.Length > 1000 ? detalle[..1000] : detalle), ct);
            metricas.ProcesoTerminado(proceso.Nombre, cronometro.Elapsed, resultadoFinal);
            return true;
        }
        finally
        {
            // Liberar un candado que no se obtuvo es un error en SQL Server.
            if (adquirido)
                await db.Database.ExecuteSqlAsync($"EXEC sp_releaseapplock @Resource = {recurso}, @LockOwner = 'Session'", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task MarcarInicioAsync(string nombre, CancellationToken ct)
    {
        var ahora = tiempo.GetUtcNow();
        var filas = await db.EjecucionesProceso.Where(e => e.Nombre == nombre).ExecuteUpdateAsync(s => s
            .SetProperty(e => e.UltimoInicio, ahora)
            .SetProperty(e => e.UltimoFin, (DateTimeOffset?)null)
            .SetProperty(e => e.UltimoResultado, (string?)null), ct);
        if (filas == 0)
        {
            db.EjecucionesProceso.Add(new EjecucionProceso { Nombre = nombre, UltimoInicio = ahora });
            await db.SaveChangesAsync(ct);
        }
    }
}
