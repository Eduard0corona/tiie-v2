using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore;

namespace Tiie.Web.Infraestructura;

public static class Observabilidad
{
    /// <summary>Nombre del medidor de las métricas propias (<c>tiie.reservas</c>, <c>tiie.procesos.duracion</c>…).</summary>
    public const string Medidor = "Tiie";

    /// <summary>Ruta del enlace de los correos; el segmento siguiente es el token de la cita.</summary>
    private static readonly PathString RutaEnlaceCita = "/mi-cita";

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

            // El token del enlace da acceso a la cita: no llega a la telemetría de solicitudes (docs/11#registros).
            // La instrumentación ya oculta los valores de la cadena de consulta.
            builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(o => o.EnrichWithHttpRequest = (actividad, solicitud) =>
            {
                if (solicitud.Path.StartsWithSegments(RutaEnlaceCita))
                    actividad.SetTag("url.path", RutaEnlaceCita + "/{token}");
            });
        }
        return builder;
    }
}
