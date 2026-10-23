using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;

namespace Tiie.Aplicacion.Reloj;

/// <summary>
/// Único lugar que decide «hoy» y «ahora» en la zona de negocio (ADR 0006). Nadie más usa el reloj
/// del sistema para reglas.
/// </summary>
public sealed class RelojAgenda(TimeProvider tiempo, IOptions<OpcionesAgenda> opciones)
{
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById(opciones.Value.ZonaHoraria);

    public TimeZoneInfo Zona => _zona;

    /// <summary>Instante actual en UTC, para marcas de tiempo.</summary>
    public DateTimeOffset AhoraUtc() => tiempo.GetUtcNow();

    /// <summary>Fecha y hora locales de la zona de negocio.</summary>
    public DateTime AhoraLocal() => TimeZoneInfo.ConvertTime(tiempo.GetUtcNow(), _zona).DateTime;

    /// <summary>Hoy en la zona de negocio, nunca la fecha del servidor.</summary>
    public DateOnly HoyLocal() => DateOnly.FromDateTime(AhoraLocal());

    /// <summary>23:59:59 locales de <paramref name="fecha"/>, expresado en UTC.</summary>
    public DateTimeOffset FinDelDia(DateOnly fecha)
    {
        var local = fecha.ToDateTime(new TimeOnly(23, 59, 59));
        return new DateTimeOffset(local, _zona.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Instante UTC que corresponde a una fecha y hora locales.</summary>
    public DateTimeOffset AUtc(DateOnly fecha, TimeOnly hora)
    {
        var local = fecha.ToDateTime(hora);
        return new DateTimeOffset(local, _zona.GetUtcOffset(local)).ToUniversalTime();
    }
}
