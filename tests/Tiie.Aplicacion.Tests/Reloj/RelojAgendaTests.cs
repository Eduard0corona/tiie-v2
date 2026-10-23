using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Reloj;

namespace Tiie.Aplicacion.Tests.Reloj;

public sealed class RelojAgendaTests
{
    private static RelojAgenda Reloj(string instanteUtc) =>
        new(new FakeTimeProvider(DateTimeOffset.Parse(instanteUtc, CultureInfo.InvariantCulture)),
            Options.Create(new OpcionesAgenda()));

    [Fact]
    public void RNF09_a_las_18_horas_de_sinaloa_hoy_sigue_siendo_el_dia_local()
    {
        // 01:00 UTC del 3 de noviembre = 18:00 del 2 de noviembre en Sinaloa (UTC-7).
        var reloj = Reloj("2026-11-03T01:00:00Z");

        reloj.HoyLocal().ShouldBe(new DateOnly(2026, 11, 2));
        reloj.AhoraLocal().ShouldBe(new DateTime(2026, 11, 2, 18, 0, 0));
    }

    [Fact]
    public void RNF09_a_medianoche_local_cambia_el_dia_aunque_en_utc_sean_las_7()
    {
        Reloj("2026-11-03T06:59:59Z").HoyLocal().ShouldBe(new DateOnly(2026, 11, 2));
        Reloj("2026-11-03T07:00:00Z").HoyLocal().ShouldBe(new DateOnly(2026, 11, 3));
    }

    [Fact]
    public void RN16_el_fin_del_dia_local_se_expresa_en_utc()
    {
        var reloj = Reloj("2026-11-03T01:00:00Z");

        reloj.FinDelDia(new DateOnly(2026, 11, 13))
            .ShouldBe(DateTimeOffset.Parse("2026-11-14T06:59:59Z", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Una_fecha_y_hora_local_se_convierte_a_utc()
    {
        var reloj = Reloj("2026-11-03T01:00:00Z");

        reloj.AUtc(new DateOnly(2026, 11, 12), new TimeOnly(10, 30))
            .ShouldBe(DateTimeOffset.Parse("2026-11-12T17:30:00Z", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Sinaloa_no_tiene_horario_de_verano()
    {
        // Desde 2022 México no cambia el horario; en julio sigue siendo UTC-7.
        Reloj("2027-07-01T06:30:00Z").HoyLocal().ShouldBe(new DateOnly(2027, 6, 30));
    }
}
