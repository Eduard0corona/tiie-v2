using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.MiCita;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.MiCita;

/// <summary>Acceso con el token del correo, consulta, cancelación, reprogramación y comprobante (RF-05, RN-13, RN-16, RN-17).</summary>
public sealed class MiCitaTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static readonly DateOnly Jueves = new(2026, 11, 5);
    private static readonly TimeOnly DiezYMedia = new(10, 30);

    private sealed record CitaPrueba(ClienteTiie Cliente, OfertaPrueba Oferta, int CupoId, string Curp);

    /// <summary>Reserva para el jueves 5 a las 10:30 con el reloj el lunes 2 a las 18:00.</summary>
    private async Task<CitaPrueba> ReservaAsync(int n = 1)
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        var cupoId = await CupoAsync(oferta.OfertaId, Jueves, DiezYMedia, capacidad: 2);
        var cliente = Fabrica.CrearCliente();
        var curp = GeneradorCurp.Valida(n);
        (await ReservarAsync(cliente, Solicitud(cupoId, curp))).StatusCode.ShouldBe(HttpStatusCode.Created);
        return new CitaPrueba(cliente, oferta, cupoId, curp);
    }

    /// <summary>Token en claro de una cita, como lo arma el proceso de correos.</summary>
    private Task<string> TokenAsync(string folio) => ConServiciosAsync(async s =>
    {
        var protegido = await s.GetRequiredService<ITiieDb>().Citas.Where(c => c.Folio == folio).Select(c => c.TokenProtegido).SingleAsync();
        return s.GetRequiredService<ITokensCita>().Recuperar(protegido)!;
    });

    private Task<string> FolioAsync() => ConDbAsync(db => db.Citas.OrderBy(c => c.CitaId).Select(c => c.Folio).FirstAsync());

    /// <summary>Vuelve a entrar con el enlace del correo: tras mover el reloj días, la cookie ya venció por inactividad.</summary>
    private async Task EntrarConEnlaceAsync(ClienteTiie cliente) =>
        (await cliente.GetAsync($"/mi-cita/{await TokenAsync(await FolioAsync())}")).Headers.Location!.OriginalString.ShouldBe("/mi-cita");

    [Fact]
    public async Task RN16_el_enlace_del_correo_cambia_el_token_por_la_cookie_y_redirige_sin_dejar_rastro()
    {
        var cita = await ReservaAsync();
        var token = await TokenAsync(await FolioAsync());
        using var otroEquipo = Fabrica.CrearCliente();

        var respuesta = await otroEquipo.GetAsync($"/mi-cita/{token}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        respuesta.Headers.Location!.OriginalString.ShouldBe("/mi-cita");
        respuesta.Headers.CacheControl!.NoStore.ShouldBeTrue();
        respuesta.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
        var detalle = await (await otroEquipo.GetAsync("/api/mi-cita")).LeerAsync<DetalleMiCita>();
        detalle.Folio.ShouldBe(await FolioAsync());
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RN16_un_token_inexistente_o_vencido_redirige_a_enlace_invalido()
    {
        var cita = await ReservaAsync();
        var token = await TokenAsync(await FolioAsync());
        using var cliente = Fabrica.CrearCliente();

        var inexistente = await cliente.GetAsync($"/mi-cita/{new string('A', 43)}");
        var malFormado = await cliente.GetAsync("/mi-cita/abc");
        FijarReloj(Jueves.AddDays(2), new TimeOnly(0, 0, 1));                // vence a las 23:59:59 del viernes
        var vencido = await cliente.GetAsync($"/mi-cita/{token}");

        foreach (var r in new[] { inexistente, malFormado, vencido })
            r.Headers.Location!.OriginalString.ShouldBe("/mi-cita?error=enlace-invalido");
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RN16_la_cookie_de_la_cita_deja_de_valer_cuando_vence_el_token()
    {
        var cita = await ReservaAsync();
        var token = await TokenAsync(await FolioAsync());
        // Diez minutos antes de que venza el token (23:59:59 del día siguiente a la cita) se entra con el enlace.
        FijarReloj(Jueves.AddDays(1), new TimeOnly(23, 50));
        using var cliente = Fabrica.CrearCliente();
        await cliente.GetAsync($"/mi-cita/{token}");
        (await cliente.GetAsync("/api/mi-cita")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // La cookie aún no vence por inactividad (30 minutos), pero el token sí.
        FijarReloj(Jueves.AddDays(2), new TimeOnly(0, 0, 30));

        (await cliente.GetAsync("/api/mi-cita")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RF05_mi_cita_muestra_la_curp_y_el_correo_enmascarados()
    {
        var cita = await ReservaAsync();

        var detalle = await (await cita.Cliente.GetAsync("/api/mi-cita")).LeerAsync<DetalleMiCita>();

        detalle.Solicitante.Curp.ShouldBe(cita.Curp[..4] + new string('*', 12) + cita.Curp[^2..]);
        detalle.Solicitante.Correo.ShouldBe("a***@example.com");
        detalle.Solicitante.NombreCompleto.ShouldBe("Ana Gómez Martínez");
        detalle.Estado.ShouldBe(EstadoCita.Agendada);
        detalle.PuedeModificar.ShouldBeTrue();
        detalle.ModificableHasta.ShouldBe(Jueves.AddDays(-1));
        detalle.OfertaId.ShouldBe(cita.Oferta.OfertaId);
        detalle.Tramite.Requisitos.Single().Texto.ShouldBe("CURP impresa.");
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RN13_cancelar_el_dia_anterior_libera_el_lugar_y_encola_el_aviso()
    {
        var cita = await ReservaAsync();
        FijarReloj(Jueves.AddDays(-1), new TimeOnly(23, 50));
        await EntrarConEnlaceAsync(cita.Cliente);

        var respuesta = await cita.Cliente.PostAsync("/api/mi-cita/cancelacion", new { motivo = "Ya no puedo ir" });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detalle = await respuesta.LeerAsync<DetalleMiCita>();
        detalle.Estado.ShouldBe(EstadoCita.Cancelada);
        detalle.PuedeModificar.ShouldBeFalse();
        (await LeerCupoAsync(cita.CupoId)).Ocupados.ShouldBe((short)0);
        var avisos = await ConDbAsync(db => db.Notificaciones.Select(n => n.Tipo).ToListAsync());
        avisos.ShouldContain(TipoNotificacion.Cancelacion);
        (await ConDbAsync(db => db.Citas.Select(c => c.MotivoCambio).SingleAsync())).ShouldBe("Ya no puedo ir");
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RN13_el_ciudadano_no_puede_cancelar_el_dia_de_la_cita()
    {
        var cita = await ReservaAsync();
        FijarReloj(Jueves, new TimeOnly(7, 0));
        await EntrarConEnlaceAsync(cita.Cliente);

        var respuesta = await cita.Cliente.PostAsync("/api/mi-cita/cancelacion", new { motivo = (string?)null });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.CitaNoModificable));
        (await (await cita.Cliente.GetAsync("/api/mi-cita")).LeerAsync<DetalleMiCita>()).PuedeModificar.ShouldBeFalse();
        (await LeerCupoAsync(cita.CupoId)).Ocupados.ShouldBe((short)1);
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RN17_reprogramar_ocupa_el_cupo_nuevo_libera_el_anterior_y_cambia_la_cookie()
    {
        var cita = await ReservaAsync();
        var folioOriginal = await FolioAsync();
        var tokenOriginal = await TokenAsync(folioOriginal);
        var viernes = await CupoAsync(cita.Oferta.OfertaId, Jueves.AddDays(1), new TimeOnly(9, 0));

        var respuesta = await cita.Cliente.PostAsync("/api/mi-cita/reprogramacion", new { cupoId = viernes });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var nueva = await respuesta.LeerAsync<DetalleMiCita>();
        nueva.Folio.ShouldNotBe(folioOriginal);
        nueva.Fecha.ShouldBe(Jueves.AddDays(1));
        (await LeerCupoAsync(cita.CupoId)).Ocupados.ShouldBe((short)0);
        (await LeerCupoAsync(viernes)).Ocupados.ShouldBe((short)1);
        var citas = await ConDbAsync(db => db.Citas.AsNoTracking().OrderBy(c => c.CitaId).ToListAsync());
        citas.Select(c => c.Estado).ShouldBe([EstadoCita.Reprogramada, EstadoCita.Agendada]);
        citas[1].CitaOrigenId.ShouldBe(citas[0].CitaId);
        (await ConDbAsync(db => db.Notificaciones.CountAsync(n => n.Tipo == TipoNotificacion.Reprogramacion && n.CitaId == citas[1].CitaId))).ShouldBe(1);
        (await (await cita.Cliente.GetAsync("/api/mi-cita")).LeerAsync<DetalleMiCita>()).Folio.ShouldBe(nueva.Folio);

        // El enlace anterior sigue sirviendo, solo para consultar la cita original.
        using var conEnlaceAnterior = Fabrica.CrearCliente();
        await conEnlaceAnterior.GetAsync($"/mi-cita/{tokenOriginal}");
        (await (await conEnlaceAnterior.GetAsync("/api/mi-cita")).LeerAsync<DetalleMiCita>()).Estado.ShouldBe(EstadoCita.Reprogramada);
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RN17_no_se_reprograma_a_otra_oferta_ni_a_un_cupo_lleno()
    {
        var cita = await ReservaAsync();
        var otraOferta = await OfertaAsync();
        var ajeno = await CupoAsync(otraOferta.OfertaId, Jueves, new TimeOnly(9, 0));
        var lleno = await CupoAsync(cita.Oferta.OfertaId, Jueves, new TimeOnly(9, 0));
        await ConDbAsync(db => db.Cupos.Where(c => c.CupoId == lleno).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)1)));

        var aOtra = await cita.Cliente.PostAsync("/api/mi-cita/reprogramacion", new { cupoId = ajeno });
        var aLleno = await cita.Cliente.PostAsync("/api/mi-cita/reprogramacion", new { cupoId = lleno });

        (await aOtra.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
        (await aLleno.CodigoAsync()).ShouldBe(nameof(CodigoError.SinCupo));
        (await ConDbAsync(db => db.Citas.SingleAsync())).Estado.ShouldBe(EstadoCita.Agendada);
        (await LeerCupoAsync(cita.CupoId)).Ocupados.ShouldBe((short)1);
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task RF04_el_comprobante_es_un_pdf_con_el_folio_en_el_nombre()
    {
        var cita = await ReservaAsync();

        var respuesta = await cita.Cliente.GetAsync("/api/mi-cita/comprobante.pdf");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        respuesta.Content.Headers.ContentDisposition!.FileName!.ShouldContain(await FolioAsync());
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        cita.Cliente.Dispose();
    }

    [Fact]
    public async Task Sin_cookie_de_cita_mi_cita_responde_401_y_salir_cierra_el_acceso()
    {
        var cita = await ReservaAsync();
        using var anonimo = Fabrica.CrearCliente();

        (await (await anonimo.GetAsync("/api/mi-cita")).CodigoAsync()).ShouldBe(nameof(CodigoError.NoAutenticado));
        (await cita.Cliente.PostAsync("/api/mi-cita/salida")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cita.Cliente.GetAsync("/api/mi-cita")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        cita.Cliente.Dispose();
    }
}
