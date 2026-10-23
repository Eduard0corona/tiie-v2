using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Reloj;

namespace Tiie.Aplicacion.Procesos;

/// <summary>
/// Corre los procesos dentro del sitio (ADR 0007): la bandeja de salida cada 15 segundos y los diarios cuando su
/// hora local ya pasó y aún no corren hoy. Si el sitio estuvo apagado, se ponen al día al arrancar.
/// </summary>
public sealed class PlanificadorProcesos(
    IServiceScopeFactory alcances,
    TimeProvider tiempo,
    RelojAgenda reloj,
    IOptions<OpcionesProcesos> opciones,
    ILogger<PlanificadorProcesos> logger)
    : BackgroundService
{
    public static readonly TimeSpan IntervaloEnvio = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan IntervaloDiarios = TimeSpan.FromMinutes(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Value.Habilitados)
        {
            logger.LogWarning("Procesos en segundo plano desactivados por Procesos:Habilitados.");
            return Task.CompletedTask;
        }
        return Task.WhenAll(
            CicloAsync(IntervaloEnvio, EnviarNotificacionesAsync, stoppingToken),
            CicloAsync(IntervaloDiarios, RevisarDiariosAsync, stoppingToken));
    }

    private async Task CicloAsync(TimeSpan intervalo, Func<CancellationToken, Task> accion, CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(intervalo, tiempo);
        do
        {
            try
            {
                await accion(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error en el planificador de procesos");
            }
        }
        while (await temporizador.WaitForNextTickAsync(ct));
    }

    private async Task EnviarNotificacionesAsync(CancellationToken ct)
    {
        await using var alcance = alcances.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<EnvioNotificaciones>().EjecutarAsync(ct);
    }

    private async Task RevisarDiariosAsync(CancellationToken ct)
    {
        var ahoraLocal = reloj.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahoraLocal);
        IReadOnlyList<(string Nombre, TimeOnly Hora, DayOfWeek? Dia)> programados;
        await using (var alcance = alcances.CreateAsyncScope())
        {
            programados = [.. alcance.ServiceProvider.GetServices<IProcesoDiario>().Select(p => (p.Nombre, p.Hora, p.Dia))];
        }

        foreach (var (nombre, hora, dia) in programados)
        {
            if ((dia is { } soloDia && hoy.DayOfWeek != soloDia) || ahoraLocal < hoy.ToDateTime(hora))
                continue;

            // Un alcance por proceso: cada uno usa su propio DbContext y su propia conexión con el candado.
            await using var alcance = alcances.CreateAsyncScope();
            var servicios = alcance.ServiceProvider;
            var proceso = servicios.GetServices<IProcesoDiario>().Single(p => p.Nombre == nombre);
            await servicios.GetRequiredService<EjecutorProcesos>().EjecutarAsync(proceso, reloj.AUtc(hoy, hora), ct);
        }
    }
}
