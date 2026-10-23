using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;

namespace Tiie.Dominio.Tests.Agenda;

public sealed class ReglasAgendaTests
{
    // Lunes 2 de noviembre de 2026, 18:00 en Sinaloa.
    private static readonly DateTime Ahora = new(2026, 11, 2, 18, 0, 0);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora);
    private static readonly TimeOnly Diez = new(10, 0);

    private static CodigoError Rechazo(Action accion) => Should.Throw<ReglaNegocioException>(accion).Codigo;

    [Fact]
    public void RN03_en_linea_la_ventana_empieza_manana_y_termina_hoy_mas_los_dias_de_la_oferta()
    {
        ReglasAgenda.PrimerDiaReservable(Hoy, OrigenCita.Web).ShouldBe(new DateOnly(2026, 11, 3));
        ReglasAgenda.UltimoDiaReservable(Hoy, 49).ShouldBe(new DateOnly(2026, 12, 21));
        ReglasAgenda.EnVentana(new DateOnly(2026, 12, 21), Hoy, 49, OrigenCita.Web).ShouldBeTrue();
        ReglasAgenda.EnVentana(new DateOnly(2026, 12, 22), Hoy, 49, OrigenCita.Web).ShouldBeFalse();
    }

    [Fact]
    public void RN03_hoy_no_se_reserva_en_linea()
    {
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy, new TimeOnly(19, 0), 49, false, true, OrigenCita.Web, Ahora))
            .ShouldBe(CodigoError.FueraDeVentana);
    }

    [Fact]
    public void RN03_fuera_de_la_ventana_se_rechaza()
    {
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy.AddDays(50), Diez, 49, false, true, OrigenCita.Web, Ahora))
            .ShouldBe(CodigoError.FueraDeVentana);
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy.AddDays(-1), Diez, 49, false, true, OrigenCita.Ventanilla, Ahora))
            .ShouldBe(CodigoError.FueraDeVentana);
    }

    [Fact]
    public void RN18_en_ventanilla_se_acepta_un_cupo_de_hoy_con_hora_futura_y_en_la_web_no()
    {
        Should.NotThrow(() => ReglasAgenda.ValidarReserva(Hoy, new TimeOnly(18, 30), 49, false, true, OrigenCita.Ventanilla, Ahora));
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy, new TimeOnly(18, 30), 49, false, true, OrigenCita.Web, Ahora))
            .ShouldBe(CodigoError.FueraDeVentana);
    }

    [Fact]
    public void RN18_en_ventanilla_una_hora_de_hoy_que_ya_paso_se_rechaza()
    {
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy, new TimeOnly(17, 30), 49, false, true, OrigenCita.Ventanilla, Ahora))
            .ShouldBe(CodigoError.FueraDeVentana);
    }

    [Fact]
    public void RN07_un_cupo_cerrado_por_bloqueo_se_rechaza()
    {
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy.AddDays(3), Diez, 49, true, true, OrigenCita.Web, Ahora))
            .ShouldBe(CodigoError.CupoCerrado);
    }

    [Fact]
    public void RN09_una_oferta_inactiva_se_rechaza_antes_que_cualquier_otra_regla()
    {
        Rechazo(() => ReglasAgenda.ValidarReserva(Hoy.AddDays(90), Diez, 49, true, false, OrigenCita.Web, Ahora))
            .ShouldBe(CodigoError.OfertaInactiva);
    }

    [Fact]
    public void RN03_el_rango_pedido_se_recorta_a_la_ventana()
    {
        var (desde, hasta) = ReglasAgenda.RecortarAVentana(Hoy.AddDays(-5), Hoy.AddDays(400), Hoy, 49);
        desde.ShouldBe(Hoy.AddDays(1));
        hasta.ShouldBe(Hoy.AddDays(49));

        var (d2, h2) = ReglasAgenda.RecortarAVentana(Hoy.AddDays(10), Hoy.AddDays(12), Hoy, 49);
        (d2, h2).ShouldBe((Hoy.AddDays(10), Hoy.AddDays(12)));
    }
}

public sealed class CalculadoraDisponibilidadTests
{
    private const double Umbral = 0.45;

    [Theory]
    [InlineData(0, 0, 0, 0, EstadoDia.SinServicio)]
    [InlineData(4, 0, 0, 0, EstadoDia.Cerrado)]
    [InlineData(4, 4, 16, 16, EstadoDia.Llena)]
    [InlineData(4, 4, 20, 11, EstadoDia.Poca)]       // 9 / 20 = 0.45
    [InlineData(4, 4, 20, 10, EstadoDia.Alta)]       // 10 / 20 = 0.50
    [InlineData(4, 2, 8, 0, EstadoDia.Alta)]         // solo cuentan los cupos abiertos
    public void El_estado_del_dia_sigue_la_tabla_de_disponibilidad(int cupos, int abiertos, int capacidad, int ocupados, EstadoDia esperado)
    {
        CalculadoraDisponibilidad.Estado(new TotalesDia(cupos, abiertos, capacidad, ocupados), Umbral).ShouldBe(esperado);
    }
}

public sealed class FolioTests
{
    [Fact]
    public void RN10_el_folio_tiene_formato_AAMMDD_NNNNNNN_con_la_fecha_local()
    {
        Folio.Formatear(new DateOnly(2026, 11, 3), 1_000_042).ShouldBe("261103-1000042");
        Folio.Formatear(new DateOnly(2027, 1, 9), Folio.PrimerNumero).ShouldBe("270109-1000000");
    }
}
