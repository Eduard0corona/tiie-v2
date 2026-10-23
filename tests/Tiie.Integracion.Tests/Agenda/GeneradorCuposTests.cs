using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Horarios;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Agenda;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Agenda;

/// <summary>Generación de cupos desde la plantilla (docs/03#generación-de-cupos).</summary>
public sealed class GeneradorCuposTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static readonly TimeOnly Nueve = new(9, 0);
    private static readonly TimeOnly Diez = new(10, 0);

    private Task<List<Cupo>> CuposAsync(int ofertaId) =>
        ConDbAsync(db => db.Cupos.AsNoTracking().Where(c => c.OfertaId == ofertaId).OrderBy(c => c.Fecha).ThenBy(c => c.Hora).ToListAsync());

    private Task CambiarPlantillaAsync(int ofertaId, params (DayOfWeek Dia, TimeOnly Hora, byte Turnos)[] filas) =>
        ConDbAsync(async db =>
        {
            await db.Plantillas.Where(p => p.OfertaId == ofertaId).ExecuteDeleteAsync();
            db.Plantillas.AddRange(filas.Select(f => new PlantillaHorario { OfertaId = ofertaId, DiaSemana = f.Dia, Hora = f.Hora, Turnos = f.Turnos }));
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task RN04_genera_un_cupo_por_hora_de_la_plantilla_de_manana_al_final_de_la_ventana()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 14);

        var resumen = await GenerarCuposAsync(oferta.OfertaId);

        var cupos = await CuposAsync(oferta.OfertaId);
        resumen.CuposCreados.ShouldBe(20);                                  // 10 días hábiles × 2 horas
        cupos.Count.ShouldBe(20);
        cupos.Min(c => c.Fecha).ShouldBe(Hoy.AddDays(1));
        cupos.Max(c => c.Fecha).ShouldBe(Hoy.AddDays(14));
        cupos.ShouldAllBe(c => c.Capacidad == 2 && c.Ocupados == 0 && !c.Cerrado);
    }

    [Fact]
    public async Task RN06_sin_filas_de_domingo_ni_sabado_esos_dias_no_tienen_cupos()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 14);

        await GenerarCuposAsync(oferta.OfertaId);

        (await CuposAsync(oferta.OfertaId)).ShouldAllBe(c => c.Fecha.DayOfWeek != DayOfWeek.Sunday && c.Fecha.DayOfWeek != DayOfWeek.Saturday);
    }

    [Fact]
    public async Task La_generacion_es_idempotente()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 14);
        await GenerarCuposAsync(oferta.OfertaId);
        var antes = await CuposAsync(oferta.OfertaId);

        var segunda = await GenerarCuposAsync(oferta.OfertaId);

        segunda.ShouldBe(ResumenGeneracion.Vacio);
        (await CuposAsync(oferta.OfertaId)).Select(c => (c.CupoId, c.Capacidad, c.Cerrado))
            .ShouldBe(antes.Select(c => (c.CupoId, c.Capacidad, c.Cerrado)));
    }

    [Fact]
    public async Task RN02_al_bajar_los_turnos_un_cupo_con_citas_conserva_capacidad_para_ellas()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7, plantilla: Laborables([Nueve], 4));
        await GenerarCuposAsync(oferta.OfertaId);
        var conCitas = (await CuposAsync(oferta.OfertaId)).First();
        await ConDbAsync(db => db.Cupos.Where(c => c.CupoId == conCitas.CupoId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)3)));

        await CambiarPlantillaAsync(oferta.OfertaId, [.. Laborables([Nueve], 2)]);
        var resumen = await GenerarCuposAsync(oferta.OfertaId);

        var cupos = await CuposAsync(oferta.OfertaId);
        cupos.Single(c => c.CupoId == conCitas.CupoId).Capacidad.ShouldBe((short)3);
        cupos.Where(c => c.CupoId != conCitas.CupoId).ShouldAllBe(c => c.Capacidad == 2);
        resumen.CuposConCitasSinCambio.ShouldBe(1);
        resumen.CuposActualizados.ShouldBe(cupos.Count);
    }

    [Fact]
    public async Task RN04_una_hora_que_sale_de_la_plantilla_se_borra_y_si_tiene_citas_se_cierra()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7);
        await GenerarCuposAsync(oferta.OfertaId);
        var conCita = (await CuposAsync(oferta.OfertaId)).First(c => c.Hora == Diez);
        await ConDbAsync(db => db.Cupos.Where(c => c.CupoId == conCita.CupoId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)1)));

        await CambiarPlantillaAsync(oferta.OfertaId, [.. Laborables([Nueve], 2)]);
        var resumen = await GenerarCuposAsync(oferta.OfertaId);

        var cupos = await CuposAsync(oferta.OfertaId);
        cupos.Where(c => c.Hora == Diez).Select(c => c.CupoId).ShouldBe([conCita.CupoId]);
        cupos.Single(c => c.CupoId == conCita.CupoId).Cerrado.ShouldBeTrue();
        resumen.CuposEliminados.ShouldBe(4);                                 // 5 días hábiles − 1 con cita
        resumen.CuposConCitasSinCambio.ShouldBe(1);
    }

    [Fact]
    public async Task RN07_los_cupos_nuevos_dentro_de_un_bloqueo_nacen_cerrados()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7);
        var admin = await UsuarioAsync(Roles.Administrador);
        await ConDbAsync(async db =>
        {
            db.Bloqueos.Add(new Bloqueo { Desde = Hoy.AddDays(2), Hasta = Hoy.AddDays(3), ModuloId = oferta.ModuloId, Motivo = "Mantenimiento", CreadoPor = admin.Id });
            await db.SaveChangesAsync();
        });

        await GenerarCuposAsync(oferta.OfertaId);

        var cupos = await CuposAsync(oferta.OfertaId);
        cupos.Where(c => c.Fecha >= Hoy.AddDays(2) && c.Fecha <= Hoy.AddDays(3)).ShouldAllBe(c => c.Cerrado);
        cupos.Where(c => c.Fecha < Hoy.AddDays(2) || c.Fecha > Hoy.AddDays(3)).ShouldAllBe(c => !c.Cerrado);
    }

    [Fact]
    public async Task Los_cupos_de_hoy_y_de_fechas_pasadas_nunca_se_modifican()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 7);
        var hoy = await CupoAsync(oferta.OfertaId, Hoy, new TimeOnly(15, 0), capacidad: 9);
        var ayer = await CupoAsync(oferta.OfertaId, Hoy.AddDays(-1), new TimeOnly(15, 0), capacidad: 9);

        await GenerarCuposAsync(oferta.OfertaId);

        (await LeerCupoAsync(hoy)).Capacidad.ShouldBe((short)9);
        (await LeerCupoAsync(ayer)).Capacidad.ShouldBe((short)9);
    }
}
