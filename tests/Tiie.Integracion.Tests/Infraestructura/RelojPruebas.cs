namespace Tiie.Integracion.Tests.Infraestructura;

/// <summary>
/// Reloj del sitio en las pruebas de integración. A diferencia de <c>FakeTimeProvider</c>, puede volver al
/// instante real al empezar cada prueba aunque la anterior lo haya adelantado horas.
/// </summary>
public sealed class RelojPruebas : TimeProvider
{
    private long _ticksUtc = DateTimeOffset.UtcNow.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticksUtc), TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Fijar(DateTimeOffset instante) => Interlocked.Exchange(ref _ticksUtc, instante.UtcTicks);

    public void Avanzar(TimeSpan lapso) => Interlocked.Add(ref _ticksUtc, lapso.Ticks);
}
