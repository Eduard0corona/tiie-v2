using System.Diagnostics.Metrics;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Comun;

/// <summary>Métricas propias (docs/09#logging-y-observabilidad). El sitio las envía a Azure Monitor.</summary>
public sealed class Metricas : IDisposable
{
    public const string NombreMedidor = "Tiie";

    private readonly Meter _medidor;
    private readonly Counter<long> _reservas;
    private readonly Counter<long> _rechazadas;
    private readonly Histogram<double> _duracionProcesos;
    private readonly Counter<long> _descartadas;
    private long _atrasadas;

    public Metricas(IMeterFactory fabrica)
    {
        ArgumentNullException.ThrowIfNull(fabrica);
        _medidor = fabrica.Create(NombreMedidor);
        _reservas = _medidor.CreateCounter<long>("tiie.reservas", description: "Citas creadas");
        _rechazadas = _medidor.CreateCounter<long>("tiie.reservas.rechazadas", description: "Reservas rechazadas por código");
        _duracionProcesos = _medidor.CreateHistogram<double>("tiie.procesos.duracion", unit: "s", description: "Duración de los procesos programados");
        _descartadas = _medidor.CreateCounter<long>("tiie.notificaciones.descartadas", description: "Notificaciones que agotaron los reintentos");
        _medidor.CreateObservableGauge("tiie.notificaciones.atrasadas", () => Interlocked.Read(ref _atrasadas),
            description: "Notificaciones que debieron enviarse hace más de 15 minutos, en la última revisión");
    }

    public void ReservaCreada(string origen) => _reservas.Add(1, new KeyValuePair<string, object?>("origen", origen));

    public void ReservaRechazada(CodigoError codigo) => _rechazadas.Add(1, new KeyValuePair<string, object?>("codigo", codigo.ToString()));

    /// <param name="resultado"><c>Correcto</c> o <c>Error</c>, como en <c>EjecucionProceso</c>.</param>
    public void ProcesoTerminado(string proceso, TimeSpan duracion, string resultado) =>
        _duracionProcesos.Record(duracion.TotalSeconds,
            new KeyValuePair<string, object?>("proceso", proceso), new KeyValuePair<string, object?>("resultado", resultado));

    public void NotificacionDescartada(string tipo) => _descartadas.Add(1, new KeyValuePair<string, object?>("tipo", tipo));

    public void NotificacionesAtrasadas(long cantidad) => Interlocked.Exchange(ref _atrasadas, cantidad);

    public void Dispose() => _medidor.Dispose();
}
