using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Panel;

/// <summary>Agenda del día, asistencia, ventanilla y cambios de citas por el personal (RF-09 a RF-11).</summary>
public sealed class CitasPanelTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static readonly DateOnly Jueves = new(2026, 11, 5);

    /// <summary>Una cita web del jueves 5 a las 10:00, reservada el lunes 2.</summary>
    private async Task<(OfertaPrueba Oferta, int CitaId, int CupoId)> CitaWebAsync(int n = 1)
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        var cupo = await CupoAsync(oferta.OfertaId, Jueves, new TimeOnly(10, 0), capacidad: 2);
        using var publico = Fabrica.CrearCliente();
        (await ReservarAsync(publico, Solicitud(cupo, GeneradorCurp.Valida(n)))).StatusCode.ShouldBe(HttpStatusCode.Created);
        var citaId = await ConDbAsync(db => db.Citas.OrderByDescending(c => c.CitaId).Select(c => c.CitaId).FirstAsync());
        return (oferta, citaId, cupo);
    }

    private static object Ventanilla(int cupoId, string curp, string? correo = null) => new
    {
        cupoId,
        curp,
        nombre = "Luis",
        apellidoPaterno = "Pérez",
        celular = (string?)null,
        correo,
        avisoLeido = true,
    };

    private static async Task<HttpResponseMessage> VentanillaAsync(ClienteTiie cliente, object cuerpo, Guid? clave = null)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/admin/citas", UriKind.Relative))
        {
            Content = JsonContent.Create(cuerpo, options: ClienteTiie.Json),
        };
        solicitud.Headers.Add("Idempotency-Key", (clave ?? Guid.NewGuid()).ToString());
        return await cliente.Http.SendAsync(solicitud);
    }

    [Fact]
    public async Task RF09_la_agenda_del_dia_muestra_las_citas_del_modulo_por_hora_sin_canceladas()
    {
        var (oferta, citaId, _) = await CitaWebAsync(1);
        var temprano = await CupoAsync(oferta.OfertaId, Jueves, new TimeOnly(8, 0));
        using (var publico = Fabrica.CrearCliente())
        {
            await ReservarAsync(publico, Solicitud(temprano, GeneradorCurp.Valida(2)));
            await ReservarAsync(publico, Solicitud(temprano, GeneradorCurp.Valida(3)));
        }
        await ConDbAsync(db => db.Citas.Where(c => c.Solicitante.Curp == GeneradorCurp.Valida(3))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.Cancelada)));
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);

        var agenda = (await capturista.Http.GetFromJsonAsync<List<CitaAgenda>>($"/api/admin/agenda?moduloId={oferta.ModuloId}&fecha=2026-11-05", ClienteTiie.Json))!;

        agenda.Select(c => c.Hora).ShouldBe([new TimeOnly(8, 0), new TimeOnly(10, 0)]);
        agenda[1].CitaId.ShouldBe(citaId);
        agenda[1].Curp.ShouldBe(GeneradorCurp.Valida(1));                       // el personal del módulo ve la CURP completa
        agenda[1].NombreCompleto.ShouldBe("Ana Gómez Martínez");
    }

    [Fact]
    public async Task RF09_la_asistencia_se_registra_el_dia_de_la_cita_y_nunca_antes()
    {
        var (oferta, citaId, _) = await CitaWebAsync();
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);

        var antes = await capturista.PostAsync($"/api/admin/citas/{citaId}/asistencia", new { asistio = true });
        FijarReloj(Jueves, new TimeOnly(10, 5));
        using var mismoDia = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);
        var registrada = await mismoDia.PostAsync($"/api/admin/citas/{citaId}/asistencia", new { asistio = true });
        var repetida = await mismoDia.PostAsync($"/api/admin/citas/{citaId}/asistencia", new { asistio = false });

        (await antes.CodigoAsync()).ShouldBe(nameof(CodigoError.CitaNoModificable));
        (await registrada.LeerAsync<EstadoResultante>()).Estado.ShouldBe(EstadoCita.Atendida);
        (await repetida.CodigoAsync()).ShouldBe(nameof(CodigoError.CitaNoModificable));
        (await ConDbAsync(db => db.Auditorias.CountAsync(a => a.Accion == "Cita.Asistencia" && a.EntidadId == citaId.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ShouldBe(1);
    }

    [Fact]
    public async Task RN12_el_supervisor_corrige_la_asistencia_dentro_de_7_dias_y_el_capturista_no()
    {
        var (oferta, citaId, _) = await CitaWebAsync();
        await ConDbAsync(db => db.Citas.ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.NoAsistio)));
        FijarReloj(Jueves.AddDays(7), new TimeOnly(9, 0));
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);

        (await capturista.PostAsync($"/api/admin/citas/{citaId}/correccion-asistencia", new { asistio = true, motivo = "Llegó tarde" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await supervisor.PostAsync($"/api/admin/citas/{citaId}/correccion-asistencia", new { asistio = true }))
            .CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
        (await (await supervisor.PostAsync($"/api/admin/citas/{citaId}/correccion-asistencia", new { asistio = true, motivo = "Llegó tarde" }))
            .LeerAsync<EstadoResultante>()).Estado.ShouldBe(EstadoCita.Atendida);

        FijarReloj(Jueves.AddDays(8), new TimeOnly(9, 0));
        using var tarde = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);
        (await (await tarde.PostAsync($"/api/admin/citas/{citaId}/correccion-asistencia", new { asistio = false, motivo = "Error" }))
            .CodigoAsync()).ShouldBe(nameof(CodigoError.CitaNoModificable));
    }

    [Fact]
    public async Task RN18_en_ventanilla_se_reserva_hoy_con_hora_futura_sin_captcha_ni_correo()
    {
        FijarReloj(Hoy, new TimeOnly(9, 0));
        var oferta = await OfertaAsync();
        var hoyFuturo = await CupoAsync(oferta.OfertaId, Hoy, new TimeOnly(12, 0));
        var hoyPasado = await CupoAsync(oferta.OfertaId, Hoy, new TimeOnly(8, 0));
        var capturistaUsuario = await UsuarioAsync(Roles.Capturista, modulos: oferta.ModuloId);
        using var capturista = await ClienteDeAsync(capturistaUsuario);

        var creada = await VentanillaAsync(capturista, Ventanilla(hoyFuturo, GeneradorCurp.Valida(10)));
        var pasada = await VentanillaAsync(capturista, Ventanilla(hoyPasado, GeneradorCurp.Valida(11)));

        creada.StatusCode.ShouldBe(HttpStatusCode.Created);
        var registrada = await creada.LeerAsync<CitaRegistrada>();
        var cita = await ConDbAsync(db => db.Citas.AsNoTracking().Include(c => c.Notificaciones).SingleAsync(c => c.CitaId == registrada.CitaId));
        cita.Origen.ShouldBe(OrigenCita.Ventanilla);
        cita.CreadaPor.ShouldBe(capturistaUsuario.Id);
        cita.Notificaciones.ShouldBeEmpty();                                 // sin correo no hay confirmación
        (await pasada.CodigoAsync()).ShouldBe(nameof(CodigoError.FueraDeVentana));
    }

    [Fact]
    public async Task RF10_ventanilla_en_un_cupo_de_otro_modulo_responde_404()
    {
        FijarReloj(Hoy, new TimeOnly(9, 0));
        var propio = await OfertaAsync();
        var ajeno = await OfertaAsync();
        var cupoAjeno = await CupoAsync(ajeno.OfertaId, Jueves, new TimeOnly(10, 0));
        using var capturista = await ClienteComoAsync(Roles.Capturista, propio.ModuloId);

        (await VentanillaAsync(capturista, Ventanilla(cupoAjeno, GeneradorCurp.Valida(12)))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RF11_el_personal_cancela_con_motivo_el_mismo_dia_antes_de_la_hora_y_se_avisa()
    {
        var (oferta, citaId, cupoId) = await CitaWebAsync();
        FijarReloj(Jueves, new TimeOnly(9, 0));
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);

        var sinMotivo = await capturista.PostAsync($"/api/admin/citas/{citaId}/cancelacion", new { motivo = "" });
        var cancelada = await capturista.PostAsync($"/api/admin/citas/{citaId}/cancelacion", new { motivo = "La persona llamó" });

        (await sinMotivo.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
        (await cancelada.LeerAsync<EstadoResultante>()).Estado.ShouldBe(EstadoCita.Cancelada);
        (await LeerCupoAsync(cupoId)).Ocupados.ShouldBe((short)0);
        (await ConDbAsync(db => db.Notificaciones.CountAsync(n => n.Tipo == TipoNotificacion.Cancelacion))).ShouldBe(1);
        (await ConDbAsync(db => db.Citas.SingleAsync())).ActualizadaPor.ShouldNotBeNull();
    }

    [Fact]
    public async Task RF11_el_personal_reprograma_a_hoy_con_hora_futura_y_reenvia_la_confirmacion()
    {
        var (oferta, citaId, cupoId) = await CitaWebAsync();
        FijarReloj(Hoy.AddDays(1), new TimeOnly(9, 0));                       // martes
        var hoyMasTarde = await CupoAsync(oferta.OfertaId, Hoy.AddDays(1), new TimeOnly(12, 0));
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);

        var nueva = await (await capturista.PostAsync($"/api/admin/citas/{citaId}/reprogramacion", new { cupoId = hoyMasTarde, motivo = "Pidió adelantar" }))
            .LeerAsync<CitaRegistrada>();
        var reenvio = await capturista.PostAsync($"/api/admin/citas/{nueva.CitaId}/reenvio");

        nueva.CitaId.ShouldNotBe(citaId);
        (await LeerCupoAsync(cupoId)).Ocupados.ShouldBe((short)0);
        (await LeerCupoAsync(hoyMasTarde)).Ocupados.ShouldBe((short)1);
        reenvio.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await ConDbAsync(db => db.Notificaciones.CountAsync(n => n.CitaId == nueva.CitaId && n.Tipo == TipoNotificacion.Confirmacion))).ShouldBe(1);
    }

    [Fact]
    public async Task RF09_la_busqueda_encuentra_por_folio_o_curp_solo_en_los_modulos_del_usuario()
    {
        var (oferta, citaId, _) = await CitaWebAsync(1);
        var folio = await ConDbAsync(db => db.Citas.Select(c => c.Folio).SingleAsync());
        var otroModulo = await ModuloAsync("Otro");
        using var propio = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);
        using var ajeno = await ClienteComoAsync(Roles.Capturista, otroModulo);

        var porFolio = await propio.Http.GetFromJsonAsync<ListaPaginada<CitaListado>>($"/api/admin/citas?busqueda={folio}", ClienteTiie.Json);
        var porCurp = await propio.Http.GetFromJsonAsync<ListaPaginada<CitaListado>>($"/api/admin/citas?busqueda={GeneradorCurp.Valida(1).ToLowerInvariant()}", ClienteTiie.Json);
        var desdeOtro = await ajeno.Http.GetFromJsonAsync<ListaPaginada<CitaListado>>($"/api/admin/citas?busqueda={folio}", ClienteTiie.Json);

        porFolio!.Elementos.Single().CitaId.ShouldBe(citaId);
        porCurp!.Total.ShouldBe(1);
        desdeOtro!.Total.ShouldBe(0);
        (await ajeno.GetAsync($"/api/admin/citas/{citaId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ajeno.PostAsync($"/api/admin/citas/{citaId}/cancelacion", new { motivo = "x" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ajeno.GetAsync($"/api/admin/citas/{citaId}/comprobante.pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ajeno.GetAsync($"/api/admin/ofertas/{oferta.OfertaId}/cupos")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ajeno.GetAsync($"/api/admin/agenda?moduloId={oferta.ModuloId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task El_detalle_incluye_notificaciones_movimientos_y_la_cita_reprogramada()
    {
        var (oferta, citaId, _) = await CitaWebAsync();
        var otro = await CupoAsync(oferta.OfertaId, Jueves.AddDays(1), new TimeOnly(9, 0));
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);
        var nueva = await (await capturista.PostAsync($"/api/admin/citas/{citaId}/reprogramacion", new { cupoId = otro, motivo = "Cambio" }))
            .LeerAsync<CitaRegistrada>();

        var detalle = await capturista.Http.GetFromJsonAsync<DetalleCitaPersonal>($"/api/admin/citas/{citaId}", ClienteTiie.Json);

        detalle!.Estado.ShouldBe(EstadoCita.Reprogramada);
        detalle.Correo.ShouldBe("ana@example.com");
        detalle.Relacionadas.Single().CitaId.ShouldBe(nueva.CitaId);
        detalle.Notificaciones.Single().Tipo.ShouldBe(TipoNotificacion.Confirmacion);
        detalle.Movimientos.Single().Accion.ShouldBe("Cita.Reprogramar");
        detalle.Movimientos.Single().Usuario.ShouldNotBeNull();
    }
}
