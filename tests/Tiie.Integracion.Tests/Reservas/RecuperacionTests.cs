using System.Net;
using Microsoft.EntityFrameworkCore;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Reservas;

/// <summary>Recuperar el enlace de la cita por correo (RF-06) sin revelar si la cita existe.</summary>
public sealed class RecuperacionTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private async Task<string> CitaAgendadaAsync()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        var cupo = await CupoAsync(oferta.OfertaId, new DateOnly(2026, 11, 5), new TimeOnly(10, 30));
        var curp = GeneradorCurp.Valida(3);
        using var cliente = Fabrica.CrearCliente();
        (await ReservarAsync(cliente, Solicitud(cupo, curp))).StatusCode.ShouldBe(HttpStatusCode.Created);
        return curp;
    }

    private static Task<HttpResponseMessage> RecuperarAsync(ClienteTiie cliente, string curp, string correo, string captcha = CaptchaPruebas.TokenValido) =>
        cliente.PostAsync("/api/publico/citas/recuperacion", new { curp, correo, tokenCaptcha = captcha });

    private Task<int> EnlacesAsync() =>
        ConDbAsync(db => db.Notificaciones.CountAsync(n => n.Tipo == TipoNotificacion.EnlaceRecuperacion));

    [Fact]
    public async Task RF06_la_respuesta_es_igual_exista_o_no_la_cita_y_solo_encola_si_coinciden_curp_y_correo()
    {
        var curp = await CitaAgendadaAsync();
        using var cliente = Fabrica.CrearCliente();

        var coincide = await RecuperarAsync(cliente, curp, "ANA@example.com");
        var otroCorreo = await RecuperarAsync(cliente, curp, "otra@example.com");
        var sinCita = await RecuperarAsync(cliente, GeneradorCurp.Valida(99), "ana@example.com");

        foreach (var respuesta in new[] { coincide, otroCorreo, sinCita })
        {
            respuesta.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await respuesta.Content.ReadAsStringAsync()).ShouldBeEmpty();
        }
        (await EnlacesAsync()).ShouldBe(1);
        (await ConDbAsync(db => db.Notificaciones.SingleAsync(n => n.Tipo == TipoNotificacion.EnlaceRecuperacion)))
            .Destinatario.ShouldBe("ana@example.com");
    }

    [Fact]
    public async Task RF06_se_envian_como_maximo_tres_enlaces_por_curp_en_una_hora()
    {
        var curp = await CitaAgendadaAsync();
        using var cliente = Fabrica.CrearCliente();

        for (var i = 0; i < 5; i++)
            (await RecuperarAsync(cliente, curp, "ana@example.com")).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await EnlacesAsync()).ShouldBe(3);

        FijarReloj(Hoy, new TimeOnly(19, 1));
        await RecuperarAsync(cliente, curp, "ana@example.com");
        (await EnlacesAsync()).ShouldBe(4);
    }

    [Fact]
    public async Task RF06_sin_captcha_valido_no_se_procesa()
    {
        var curp = await CitaAgendadaAsync();
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await RecuperarAsync(cliente, curp, "ana@example.com", captcha: "falso");

        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.CaptchaInvalido));
        (await EnlacesAsync()).ShouldBe(0);
    }
}
