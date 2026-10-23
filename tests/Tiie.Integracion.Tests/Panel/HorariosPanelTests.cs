using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Horarios;
using Tiie.Aplicacion.Publico;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Panel;

/// <summary>Plantillas, ajuste de cupos y bloqueos (RF-14, RF-15, RN-07, RN-08).</summary>
public sealed class HorariosPanelTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static readonly DateOnly Jueves = new(2026, 11, 5);

    [Fact]
    public async Task RF14_guardar_la_plantilla_regenera_los_cupos_y_la_disponibilidad_publica_cambia()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7, plantilla: []);
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);
        using var publico = Fabrica.CrearCliente();
        var antes = await publico.Http.GetFromJsonAsync<DisponibilidadOferta>($"/api/publico/ofertas/{oferta.OfertaId}/disponibilidad", ClienteTiie.Json);

        var resumen = await (await supervisor.Http.PutAsJsonAsync($"/api/admin/ofertas/{oferta.OfertaId}/plantilla", new
        {
            dias = new object[]
            {
                new { diaSemana = 4, horas = new[] { new { hora = "08:00", turnos = 4 }, new { hora = "08:30", turnos = 4 } } },
                new { diaSemana = 6, horas = new[] { new { hora = "09:00", turnos = 3 } } },
            },
        }, ClienteTiie.Json)).LeerAsync<ResumenGeneracion>();
        var plantilla = await supervisor.Http.GetFromJsonAsync<PlantillaSemanal>($"/api/admin/ofertas/{oferta.OfertaId}/plantilla", ClienteTiie.Json);
        var despues = await publico.Http.GetFromJsonAsync<DisponibilidadOferta>($"/api/publico/ofertas/{oferta.OfertaId}/disponibilidad", ClienteTiie.Json);

        resumen.ShouldBe(new ResumenGeneracion(3, 0, 0, 0));                  // jueves 2 horas y sábado 1
        plantilla!.Dias.Select(d => d.DiaSemana).ShouldBe([(byte)4, (byte)6]);
        antes!.Dias.ShouldAllBe(d => d.Estado == EstadoDia.SinServicio);
        despues!.Dias.Single(d => d.Fecha == Jueves).ShouldBe(new DiaDisponible(Jueves, EstadoDia.Alta, 8, 8, null));
        (await ConDbAsync(db => db.Auditorias.CountAsync(a => a.Accion == "Plantilla.Guardar"))).ShouldBe(1);
    }

    [Fact]
    public async Task RF14_una_hora_repetida_en_el_mismo_dia_responde_Validacion_y_el_capturista_no_puede_guardar()
    {
        var oferta = await OfertaAsync();
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);
        var plantilla = new { dias = new[] { new { diaSemana = 1, horas = new[] { new { hora = "08:00", turnos = 1 }, new { hora = "08:00", turnos = 2 } } } } };

        (await (await supervisor.Http.PutAsJsonAsync($"/api/admin/ofertas/{oferta.OfertaId}/plantilla", plantilla, ClienteTiie.Json)).CodigoAsync())
            .ShouldBe(nameof(CodigoError.Validacion));
        (await capturista.Http.PutAsJsonAsync($"/api/admin/ofertas/{oferta.OfertaId}/plantilla", plantilla, ClienteTiie.Json)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await capturista.GetAsync($"/api/admin/ofertas/{oferta.OfertaId}/plantilla")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RN02_la_capacidad_de_un_cupo_no_baja_de_los_lugares_ocupados_y_exige_la_version_vigente()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        var cupoId = await CupoAsync(oferta.OfertaId, Jueves, new TimeOnly(10, 0), capacidad: 3);
        await ConDbAsync(db => db.Cupos.Where(c => c.CupoId == cupoId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)2)));
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);
        var cupo = (await supervisor.Http.GetFromJsonAsync<List<CupoAdmin>>($"/api/admin/ofertas/{oferta.OfertaId}/cupos", ClienteTiie.Json))!.Single();

        var menor = await supervisor.Http.PatchAsJsonAsync($"/api/admin/cupos/{cupoId}", new { capacidad = (short)1, version = cupo.Version }, ClienteTiie.Json);
        var valido = await supervisor.Http.PatchAsJsonAsync($"/api/admin/cupos/{cupoId}", new { capacidad = (short)5, cerrado = true, version = cupo.Version }, ClienteTiie.Json);
        var viejo = await supervisor.Http.PatchAsJsonAsync($"/api/admin/cupos/{cupoId}", new { capacidad = (short)6, version = cupo.Version }, ClienteTiie.Json);

        (await menor.ProblemaAsync()).GetProperty("errors").GetProperty("capacidad")[0].GetString()!.ShouldContain("2 lugares ocupados");
        var ajustado = await valido.LeerAsync<CupoAdmin>();
        (ajustado.Capacidad, ajustado.Ocupados, ajustado.Cerrado).ShouldBe(((short)5, (short)2, true));
        (await viejo.CodigoAsync()).ShouldBe(nameof(CodigoError.Conflicto));
    }

    [Fact]
    public async Task RN08_un_dia_inhabil_general_solo_lo_crea_el_administrador()
    {
        var oferta = await OfertaAsync();
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var general = new { desde = "2026-12-12", hasta = "2026-12-12", motivo = "Día inhábil" };

        (await (await supervisor.PostAsync("/api/admin/bloqueos", general)).CodigoAsync()).ShouldBe(nameof(CodigoError.Prohibido));
        (await admin.PostAsync("/api/admin/bloqueos", general)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await supervisor.PostAsync("/api/admin/bloqueos", new { desde = "2026-12-13", hasta = "2026-12-14", moduloId = oferta.ModuloId, motivo = "Fumigación" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RF15_un_bloqueo_cierra_cupos_lista_las_citas_afectadas_y_puede_cancelarlas_con_aviso()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7);
        await GenerarCuposAsync(oferta.OfertaId);
        var cupoJueves = await ConDbAsync(db => db.Cupos.Where(c => c.Fecha == Jueves && c.Hora == new TimeOnly(9, 0)).Select(c => c.CupoId).SingleAsync());
        using (var publico = Fabrica.CrearCliente())
            (await ReservarAsync(publico, Solicitud(cupoJueves, GeneradorCurp.Valida(1)))).StatusCode.ShouldBe(HttpStatusCode.Created);
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);

        var creado = await (await supervisor.PostAsync("/api/admin/bloqueos",
            new { desde = "2026-11-05", hasta = "2026-11-06", moduloId = oferta.ModuloId, motivo = "Mantenimiento" })).LeerAsync<BloqueoCreado>();

        creado.CuposCerrados.ShouldBe(4);
        creado.CitasAfectadas.Single().Fecha.ShouldBe(Jueves);
        (await ConDbAsync(db => db.Citas.SingleAsync())).Estado.ShouldBe(EstadoCita.Agendada);   // crear no cancela

        var canceladas = await (await supervisor.PostAsync($"/api/admin/bloqueos/{creado.BloqueoId}/cancelacion-citas", new { motivo = "Mantenimiento" }))
            .LeerAsync<CitasCanceladasPorBloqueo>();
        canceladas.CitasCanceladas.ShouldBe(1);
        (await ConDbAsync(db => db.Citas.SingleAsync())).Estado.ShouldBe(EstadoCita.Cancelada);
        (await ConDbAsync(db => db.Notificaciones.CountAsync(n => n.Tipo == TipoNotificacion.CierrePorBloqueo))).ShouldBe(1);
        (await LeerCupoAsync(cupoJueves)).Ocupados.ShouldBe((short)0);
    }

    [Fact]
    public async Task RN07_eliminar_un_bloqueo_reabre_solo_los_cupos_que_no_cubre_otro_bloqueo()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7);
        await GenerarCuposAsync(oferta.OfertaId);
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var modulo = await (await admin.PostAsync("/api/admin/bloqueos", new { desde = "2026-11-03", hasta = "2026-11-05", moduloId = oferta.ModuloId, motivo = "Obra" }))
            .LeerAsync<BloqueoCreado>();
        await admin.PostAsync("/api/admin/bloqueos", new { desde = "2026-11-05", hasta = "2026-11-05", motivo = "Día inhábil" });

        (await admin.Http.DeleteAsync(new Uri($"/api/admin/bloqueos/{modulo.BloqueoId}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var cerrados = await ConDbAsync(db => db.Cupos.Where(c => c.Cerrado).Select(c => c.Fecha).Distinct().ToListAsync());
        cerrados.ShouldBe([Jueves]);
    }

    [Fact]
    public async Task El_personal_solo_ve_bloqueos_generales_y_de_sus_modulos()
    {
        var propio = await OfertaAsync();
        var ajeno = await OfertaAsync();
        using var admin = await ClienteComoAsync(Roles.Administrador);
        await admin.PostAsync("/api/admin/bloqueos", new { desde = "2026-12-01", hasta = "2026-12-01", motivo = "General" });
        await admin.PostAsync("/api/admin/bloqueos", new { desde = "2026-12-02", hasta = "2026-12-02", moduloId = propio.ModuloId, motivo = "Propio" });
        await admin.PostAsync("/api/admin/bloqueos", new { desde = "2026-12-03", hasta = "2026-12-03", ofertaId = ajeno.OfertaId, motivo = "Ajeno" });
        using var capturista = await ClienteComoAsync(Roles.Capturista, propio.ModuloId);

        var lista = await capturista.Http.GetFromJsonAsync<List<BloqueoResumen>>("/api/admin/bloqueos", ClienteTiie.Json);

        lista!.Select(b => b.Motivo).Order().ShouldBe(["General", "Propio"]);
    }
}
