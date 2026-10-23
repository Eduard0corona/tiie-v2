using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace Tiie.Web.Infraestructura;

public static class Observabilidad
{
    /// <summary>Nombre del medidor de las métricas propias (<c>tiie.reservas</c>, <c>tiie.procesos.duracion</c>…).</summary>
    public const string Medidor = "Tiie";

    /// <summary>
    /// OpenTelemetry hacia Azure Monitor cuando existe <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>.
    /// En desarrollo y en pruebas los logs se quedan en la consola.
    /// </summary>
    public static WebApplicationBuilder AgregarObservabilidad(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry()
                .UseAzureMonitor()
                .WithMetrics(m => m.AddMeter(Medidor));
        }
        return builder;
    }
}
