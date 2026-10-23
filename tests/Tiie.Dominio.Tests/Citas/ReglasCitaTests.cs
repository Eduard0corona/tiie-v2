using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;

namespace Tiie.Dominio.Tests.Citas;

public sealed class ReglasCitaTests
{
    private static readonly DateOnly FechaCita = new(2026, 11, 12);

    [Fact]
    public void RN13_el_ciudadano_puede_cancelar_hasta_el_dia_anterior()
    {
        ReglasCita.ModificableHasta(FechaCita, 1).ShouldBe(new DateOnly(2026, 11, 11));
        ReglasCita.CiudadanoPuedeModificar(EstadoCita.Agendada, FechaCita, new DateOnly(2026, 11, 11), 1).ShouldBeTrue();
    }

    [Fact]
    public void RN13_el_ciudadano_no_puede_cancelar_el_dia_de_la_cita()
    {
        Should.Throw<ReglaNegocioException>(() =>
                ReglasCita.ExigirModificablePorCiudadano(EstadoCita.Agendada, FechaCita, FechaCita, 1))
            .Codigo.ShouldBe(CodigoError.CitaNoModificable);
    }

    [Fact]
    public void RN13_una_cita_que_no_esta_agendada_no_se_modifica()
    {
        ReglasCita.CiudadanoPuedeModificar(EstadoCita.Cancelada, FechaCita, new DateOnly(2026, 11, 1), 1).ShouldBeFalse();
    }

    [Fact]
    public void RN13_el_personal_si_puede_cancelar_el_dia_de_la_cita_antes_de_la_hora()
    {
        var hora = new TimeOnly(10, 30);
        ReglasCita.PersonalPuedeModificar(EstadoCita.Agendada, FechaCita, hora, FechaCita.ToDateTime(new TimeOnly(9, 0))).ShouldBeTrue();
        ReglasCita.PersonalPuedeModificar(EstadoCita.Agendada, FechaCita, hora, FechaCita.ToDateTime(new TimeOnly(10, 30))).ShouldBeFalse();
    }

    [Fact]
    public void RF09_no_se_registra_la_asistencia_de_una_cita_futura()
    {
        Should.NotThrow(() => ReglasCita.ExigirAsistenciaRegistrable(EstadoCita.Agendada, FechaCita, FechaCita));
        Should.NotThrow(() => ReglasCita.ExigirAsistenciaRegistrable(EstadoCita.Agendada, FechaCita, FechaCita.AddDays(3)));
        Should.Throw<ReglaNegocioException>(() => ReglasCita.ExigirAsistenciaRegistrable(EstadoCita.Agendada, FechaCita, FechaCita.AddDays(-1)))
            .Codigo.ShouldBe(CodigoError.CitaNoModificable);
        Should.Throw<ReglaNegocioException>(() => ReglasCita.ExigirAsistenciaRegistrable(EstadoCita.Cancelada, FechaCita, FechaCita));
    }

    [Fact]
    public void RN12_la_correccion_de_asistencia_se_permite_7_dias_despues_de_la_cita()
    {
        Should.NotThrow(() => ReglasCita.ExigirCorreccionPermitida(EstadoCita.Atendida, FechaCita, FechaCita.AddDays(7)));
        Should.NotThrow(() => ReglasCita.ExigirCorreccionPermitida(EstadoCita.NoAsistio, FechaCita, FechaCita.AddDays(1)));
        Should.Throw<ReglaNegocioException>(() => ReglasCita.ExigirCorreccionPermitida(EstadoCita.Atendida, FechaCita, FechaCita.AddDays(8)));
        Should.Throw<ReglaNegocioException>(() => ReglasCita.ExigirCorreccionPermitida(EstadoCita.Agendada, FechaCita, FechaCita));
    }
}

public sealed class NotificacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 3, 17, 0, 0, TimeSpan.Zero);

    private static Notificacion Nueva() => Notificacion.Nueva(1, TipoNotificacion.Confirmacion, "ana@example.com", Ahora);

    [Fact]
    public void RF04_los_reintentos_esperan_1_5_15_60_y_240_minutos()
    {
        var notificacion = Nueva();
        var esperas = new List<TimeSpan>();
        for (var i = 0; i < 5; i++)
        {
            notificacion.RegistrarFallo("Proveedor caído", Ahora).ShouldBeFalse();
            esperas.Add(notificacion.ProximoIntentoEn - Ahora);
        }

        esperas.ShouldBe([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(4)]);
        notificacion.Estado.ShouldBe(EstadoNotificacion.Fallida);
    }

    [Fact]
    public void RF04_al_sexto_fallo_la_notificacion_se_descarta()
    {
        var notificacion = Nueva();
        for (var i = 0; i < 5; i++)
            notificacion.RegistrarFallo("Proveedor caído", Ahora);

        notificacion.RegistrarFallo("Proveedor caído", Ahora).ShouldBeTrue();
        notificacion.Estado.ShouldBe(EstadoNotificacion.Descartada);
        notificacion.Intentos.ShouldBe((byte)6);
    }

    [Fact]
    public void Enviar_limpia_el_ultimo_error_y_registra_el_instante()
    {
        var notificacion = Nueva();
        notificacion.RegistrarFallo("Proveedor caído", Ahora);

        notificacion.MarcarEnviada(Ahora.AddMinutes(2));

        notificacion.Estado.ShouldBe(EstadoNotificacion.Enviada);
        notificacion.EnviadaEn.ShouldBe(Ahora.AddMinutes(2));
        notificacion.UltimoError.ShouldBeNull();
    }
}
