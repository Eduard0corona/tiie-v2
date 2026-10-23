using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Reservas;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Reservas;

/// <summary>Reserva en línea (RF-03): atómica, idempotente y con todas sus reglas en el servidor.</summary>
public sealed partial class ReservaTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static readonly DateOnly Jueves = new(2026, 11, 5);
    private static readonly TimeOnly DiezYMedia = new(10, 30);

    private async Task<(OfertaPrueba Oferta, int CupoId)> PrepararAsync(short capacidad = 1)
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        return (oferta, await CupoAsync(oferta.OfertaId, Jueves, DiezYMedia, capacidad));
    }

    [Fact]
    public async Task RF03_reservar_crea_la_cita_ocupa_el_lugar_inicia_mi_cita_y_encola_la_confirmacion()
    {
        var (_, cupoId) = await PrepararAsync();
        using var cliente = Fabrica.CrearCliente();
        var curp = GeneradorCurp.Valida(1);

        var respuesta = await ReservarAsync(cliente, Solicitud(cupoId, curp.ToLowerInvariant()));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);
        respuesta.Headers.Location!.OriginalString.ShouldBe("/api/mi-cita");
        var cookie = respuesta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("tiie_cita=", StringComparison.Ordinal));
        cookie.ShouldContain("path=/api/mi-cita");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("samesite=lax");

        var cita = await respuesta.LeerAsync<CitaReservada>();
        FolioDeHoy().IsMatch(cita.Folio).ShouldBeTrue(cita.Folio);
        cita.Estado.ShouldBe(EstadoCita.Agendada);
        (cita.Fecha, cita.Hora).ShouldBe((Jueves, DiezYMedia));
        (await cliente.GetAsync("/api/mi-cita")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await LeerCupoAsync(cupoId)).Ocupados.ShouldBe((short)1);
        var guardada = await ConDbAsync(db => db.Citas.AsNoTracking()
            .Include(c => c.Solicitante).Include(c => c.Notificaciones).SingleAsync());
        guardada.Origen.ShouldBe(OrigenCita.Web);
        guardada.VersionAvisoPrivacidad.ShouldBe("2026-01");
        guardada.AvisoAceptadoEn.ShouldNotBeNull();
        guardada.Solicitante.Curp.ShouldBe(curp);
        guardada.Solicitante.FechaNacimiento.ShouldBe(new DateOnly(2000, 1, 2));    // RN-15: derivada de la CURP
        guardada.Solicitante.Sexo.ShouldBe('M');
        guardada.TokenVenceEn.ShouldBe(Local(Jueves.AddDays(1), new TimeOnly(23, 59, 59)));   // RN-16
        guardada.Notificaciones.Single().Tipo.ShouldBe(TipoNotificacion.Confirmacion);
    }

    [Fact]
    public async Task RN02_cien_reservas_simultaneas_por_el_ultimo_lugar_crean_una_sola_cita()
    {
        var (_, cupoId) = await PrepararAsync(capacidad: 1);
        var clientes = Enumerable.Range(0, 100).Select(_ => Fabrica.CrearCliente()).ToList();

        var respuestas = await Task.WhenAll(clientes.Select((c, i) => ReservarAsync(c, Solicitud(cupoId, GeneradorCurp.Valida(100 + i)))));

        respuestas.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(99);
        (await Task.WhenAll(respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict).Select(r => r.CodigoAsync())))
            .ShouldAllBe(c => c == nameof(CodigoError.SinCupo));
        (await LeerCupoAsync(cupoId)).Ocupados.ShouldBe((short)1);
        (await ConDbAsync(db => db.Citas.CountAsync())).ShouldBe(1);
        clientes.ForEach(c => c.Dispose());
    }

    [Fact]
    public async Task RN01_dos_reservas_simultaneas_de_la_misma_curp_en_cupos_distintos_crean_una_sola_cita()
    {
        var (oferta, cupoA) = await PrepararAsync();
        var cupoB = await CupoAsync(oferta.OfertaId, Jueves, new TimeOnly(11, 30));
        var curp = GeneradorCurp.Valida(7);
        using var c1 = Fabrica.CrearCliente();
        using var c2 = Fabrica.CrearCliente();

        var respuestas = await Task.WhenAll(ReservarAsync(c1, Solicitud(cupoA, curp)), ReservarAsync(c2, Solicitud(cupoB, curp)));

        respuestas.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        (await respuestas.Single(r => r.StatusCode != HttpStatusCode.Created).CodigoAsync()).ShouldBe(nameof(CodigoError.YaTieneCita));
        (await ConDbAsync(db => db.Citas.CountAsync())).ShouldBe(1);
        (await ConDbAsync(db => db.Cupos.SumAsync(c => c.Ocupados))).ShouldBe(1);
    }

    [Fact]
    public async Task RF03_la_misma_Idempotency_Key_devuelve_la_misma_cita_y_ocupa_un_solo_lugar()
    {
        var (_, cupoId) = await PrepararAsync(capacidad: 5);
        var clave = Guid.NewGuid();
        var solicitud = Solicitud(cupoId, GeneradorCurp.Valida(8));

        var simultaneas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
        {
            var cliente = Fabrica.CrearCliente();
            return ReservarAsync(cliente, solicitud, clave);
        }));
        using var cliente = Fabrica.CrearCliente();
        var repetida = await ReservarAsync(cliente, solicitud, clave);

        var folios = await Task.WhenAll(simultaneas.Append(repetida).Select(async r =>
        {
            r.StatusCode.ShouldBe(HttpStatusCode.Created);
            return (await r.LeerAsync<CitaReservada>()).Folio;
        }));
        folios.Distinct().Count().ShouldBe(1);
        (await LeerCupoAsync(cupoId)).Ocupados.ShouldBe((short)1);
        (await ConDbAsync(db => db.Citas.CountAsync())).ShouldBe(1);
    }

    [Fact]
    public async Task RN01_una_curp_con_cita_agendada_recibe_YaTieneCita_y_no_ocupa_lugar()
    {
        var (oferta, cupoId) = await PrepararAsync(capacidad: 3);
        var curp = GeneradorCurp.Valida(9);
        using var cliente = Fabrica.CrearCliente();
        await ReservarAsync(cliente, Solicitud(cupoId, curp));

        var otra = await ReservarAsync(cliente, Solicitud(await CupoAsync(oferta.OfertaId, Jueves, new TimeOnly(12, 0)), curp));

        otra.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await otra.CodigoAsync()).ShouldBe(nameof(CodigoError.YaTieneCita));
        (await ConDbAsync(db => db.Cupos.SumAsync(c => c.Ocupados))).ShouldBe(1);
    }

    [Fact]
    public async Task RN01_una_curp_cuya_cita_se_cancelo_puede_volver_a_reservar()
    {
        var (_, cupoId) = await PrepararAsync(capacidad: 3);
        var curp = GeneradorCurp.Valida(10);
        using var cliente = Fabrica.CrearCliente();
        await ReservarAsync(cliente, Solicitud(cupoId, curp));
        await ConDbAsync(db => db.Citas.ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.Cancelada)));

        (await ReservarAsync(cliente, Solicitud(cupoId, curp))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ConDbAsync(db => db.Solicitantes.CountAsync())).ShouldBe(1);
    }

    [Fact]
    public async Task RN10_cada_cita_tiene_un_folio_distinto_con_la_fecha_local_de_creacion()
    {
        var (_, cupoId) = await PrepararAsync(capacidad: 3);
        using var cliente = Fabrica.CrearCliente();

        var folios = new List<string>();
        for (var i = 0; i < 3; i++)
            folios.Add((await (await ReservarAsync(cliente, Solicitud(cupoId, GeneradorCurp.Valida(20 + i)))).LeerAsync<CitaReservada>()).Folio);

        folios.Distinct().Count().ShouldBe(3);
        folios.ShouldAllBe(f => FolioDeHoy().IsMatch(f));
    }

    [Fact]
    public async Task RN02_un_cupo_lleno_responde_SinCupo()
    {
        var (_, cupoId) = await PrepararAsync(capacidad: 1);
        using var cliente = Fabrica.CrearCliente();
        await ReservarAsync(cliente, Solicitud(cupoId, GeneradorCurp.Valida(30)));

        var respuesta = await ReservarAsync(cliente, Solicitud(cupoId, GeneradorCurp.Valida(31)));

        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.SinCupo));
    }

    [Fact]
    public async Task RN03_hoy_y_fuera_de_la_ventana_responden_FueraDeVentana()
    {
        var (oferta, _) = await PrepararAsync();
        var hoy = await CupoAsync(oferta.OfertaId, Hoy, new TimeOnly(19, 0));
        var lejos = await CupoAsync(oferta.OfertaId, Hoy.AddDays(50), DiezYMedia);
        using var cliente = Fabrica.CrearCliente();

        (await (await ReservarAsync(cliente, Solicitud(hoy, GeneradorCurp.Valida(40)))).CodigoAsync()).ShouldBe(nameof(CodigoError.FueraDeVentana));
        (await (await ReservarAsync(cliente, Solicitud(lejos, GeneradorCurp.Valida(41)))).CodigoAsync()).ShouldBe(nameof(CodigoError.FueraDeVentana));
    }

    [Fact]
    public async Task RN07_un_cupo_cerrado_responde_CupoCerrado()
    {
        var (oferta, _) = await PrepararAsync();
        var cerrado = await CupoAsync(oferta.OfertaId, Jueves, new TimeOnly(12, 0), cerrado: true);
        using var cliente = Fabrica.CrearCliente();

        (await (await ReservarAsync(cliente, Solicitud(cerrado, GeneradorCurp.Valida(42)))).CodigoAsync()).ShouldBe(nameof(CodigoError.CupoCerrado));
    }

    [Fact]
    public async Task RN09_si_el_tipo_de_tramite_esta_inactivo_responde_OfertaInactiva()
    {
        var (oferta, cupoId) = await PrepararAsync();
        await ConDbAsync(db => db.TiposTramite.Where(t => t.TipoTramiteId == oferta.TipoTramiteId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, false)));
        using var cliente = Fabrica.CrearCliente();

        (await (await ReservarAsync(cliente, Solicitud(cupoId, GeneradorCurp.Valida(43)))).CodigoAsync()).ShouldBe(nameof(CodigoError.OfertaInactiva));
    }

    [Fact]
    public async Task RN15_una_curp_invalida_responde_Validacion_en_el_campo_curp()
    {
        var (_, cupoId) = await PrepararAsync();
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await ReservarAsync(cliente, Solicitud(cupoId, "GOMA080515MSLRRNA6"));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await respuesta.ProblemaAsync()).GetProperty("errors").GetProperty("curp")[0].GetString().ShouldBe("La CURP no es válida.");
        (await LeerCupoAsync(cupoId)).Ocupados.ShouldBe((short)0);
    }

    [Fact]
    public async Task RF03_sin_correo_ni_celular_ni_aviso_aceptado_responde_Validacion()
    {
        var (_, cupoId) = await PrepararAsync();
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await ReservarAsync(cliente, new
        {
            cupoId,
            curp = GeneradorCurp.Valida(44),
            nombre = "Ana",
            apellidoPaterno = "Gómez",
            celular = "123",
            correo = (string?)null,
            aceptaAvisoPrivacidad = false,
            tokenCaptcha = CaptchaPruebas.TokenValido,
        });

        var errores = (await respuesta.ProblemaAsync()).GetProperty("errors");
        errores.TryGetProperty("correo", out _).ShouldBeTrue();
        errores.TryGetProperty("celular", out _).ShouldBeTrue();
        errores.TryGetProperty("aceptaAvisoPrivacidad", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task RF03_sin_captcha_valido_responde_CaptchaInvalido_y_no_toca_la_base()
    {
        var (_, cupoId) = await PrepararAsync();
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await ReservarAsync(cliente, Solicitud(cupoId, GeneradorCurp.Valida(45), captcha: "falso"));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.CaptchaInvalido));
        (await ConDbAsync(db => db.Citas.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task RF03_sin_Idempotency_Key_responde_SolicitudInvalida()
    {
        var (_, cupoId) = await PrepararAsync();
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/api/publico/citas", Solicitud(cupoId, GeneradorCurp.Valida(46)));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.SolicitudInvalida));
    }

    [Fact]
    public async Task Los_logs_de_una_reserva_no_contienen_CURP_correo_celular_ni_nombre()
    {
        var (_, cupoId) = await PrepararAsync();
        using var cliente = Fabrica.CrearCliente();
        var curp = GeneradorCurp.Valida(47);

        (await ReservarAsync(cliente, Solicitud(cupoId, curp))).StatusCode.ShouldBe(HttpStatusCode.Created);
        await ReservarAsync(cliente, Solicitud(cupoId, curp));    // también el rechazo

        Fabrica.Logs.Mensajes.ShouldContain(m => m.Contains("creada en el cupo", StringComparison.Ordinal));
        Fabrica.Logs.Mensajes.ShouldAllBe(m =>
            !m.Contains(curp, StringComparison.OrdinalIgnoreCase)
            && !m.Contains("ana@example.com", StringComparison.OrdinalIgnoreCase)
            && !m.Contains("6671234567", StringComparison.Ordinal)
            && !m.Contains("Gómez", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"^261102-\d{7}$")]
    private static partial Regex FolioDeHoy();
}
