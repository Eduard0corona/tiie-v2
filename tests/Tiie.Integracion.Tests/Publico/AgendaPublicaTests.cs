using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Publico;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Errores;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Publico;

/// <summary>Catálogo público, disponibilidad por día y cupos (RF-01, RF-02).</summary>
public sealed class AgendaPublicaTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    [Fact]
    public async Task RF01_el_catalogo_publico_omite_modulos_tipos_y_ofertas_inactivos()
    {
        var activo = await ModuloAsync("Módulo activo");
        var inactivo = await ModuloAsync("Módulo cerrado");
        var visible = await OfertaAsync(moduloId: activo, tramite: "Primera vez");
        await OfertaAsync(moduloId: inactivo, tramite: "Solo en el módulo cerrado");
        var tipoInactivo = await OfertaAsync(moduloId: activo, tramite: "Tipo inactivo");
        await ConDbAsync(async db =>
        {
            await db.Modulos.Where(m => m.ModuloId == inactivo).ExecuteUpdateAsync(s => s.SetProperty(m => m.Activo, false));
            await db.TiposTramite.Where(t => t.TipoTramiteId == tipoInactivo.TipoTramiteId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, false));
        });
        using var cliente = Fabrica.CrearCliente();

        var tipos = (await cliente.Http.GetFromJsonAsync<List<TipoTramitePublico>>("/api/publico/tipos-tramite", ClienteTiie.Json))!;
        var modulos = (await cliente.Http.GetFromJsonAsync<List<ModuloPublico>>("/api/publico/modulos", ClienteTiie.Json))!;
        var ofertas = (await cliente.Http.GetFromJsonAsync<List<OfertaPublica>>($"/api/publico/modulos/{activo}/ofertas", ClienteTiie.Json))!;
        var municipios = (await cliente.Http.GetFromJsonAsync<List<MunicipioPublico>>("/api/publico/municipios", ClienteTiie.Json))!;

        tipos.Select(t => t.Nombre).ShouldBe(["Primera vez"]);
        tipos[0].Requisitos.Single().Texto.ShouldBe("CURP impresa.");
        modulos.Select(m => m.Nombre).ShouldBe(["Módulo activo"]);
        modulos[0].Direccion.ShouldBe("Calle S/N, Col. Centro, C.P. 80000");
        ofertas.Select(o => o.OfertaId).ShouldBe([visible.OfertaId]);
        municipios.Select(m => m.Nombre).ShouldBe(["Culiacán"]);
        (await cliente.GetAsync($"/api/publico/modulos/{inactivo}/ofertas")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await cliente.GetAsync($"/api/publico/ofertas/{tipoInactivo.OfertaId}/disponibilidad")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RF02_la_disponibilidad_marca_cada_estado_del_dia_dentro_de_la_ventana()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));                    // lunes; ventana del martes 3 al lunes 9
        var oferta = await OfertaAsync(diasVentana: 7);
        await GenerarCuposAsync(oferta.OfertaId);
        var admin = await UsuarioAsync(Roles.Administrador);
        await ConDbAsync(async db =>
        {
            // Martes lleno, miércoles con 1 de 4 lugares libres y viernes día inhábil.
            await db.Cupos.Where(c => c.Fecha == Hoy.AddDays(1)).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, c => c.Capacidad));
            await db.Cupos.Where(c => c.Fecha == Hoy.AddDays(2) && c.Hora == new TimeOnly(9, 0)).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)2));
            await db.Cupos.Where(c => c.Fecha == Hoy.AddDays(2) && c.Hora == new TimeOnly(10, 0)).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)1));
            db.Bloqueos.Add(new Bloqueo { Desde = Hoy.AddDays(4), Hasta = Hoy.AddDays(4), Motivo = "Día inhábil", CreadoPor = admin.Id });
            await db.SaveChangesAsync();
            await db.Cupos.Where(c => c.Fecha == Hoy.AddDays(4)).ExecuteUpdateAsync(s => s.SetProperty(c => c.Cerrado, true));
        });
        using var cliente = Fabrica.CrearCliente();

        var disponibilidad = await cliente.Http.GetFromJsonAsync<DisponibilidadOferta>(
            $"/api/publico/ofertas/{oferta.OfertaId}/disponibilidad", ClienteTiie.Json);

        disponibilidad!.Desde.ShouldBe(Hoy.AddDays(1));
        disponibilidad.Hasta.ShouldBe(Hoy.AddDays(7));
        disponibilidad.Dias.Select(d => d.Estado).ShouldBe([
            EstadoDia.Llena, EstadoDia.Poca, EstadoDia.Alta, EstadoDia.Cerrado, EstadoDia.SinServicio, EstadoDia.SinServicio, EstadoDia.Alta]);
        disponibilidad.Dias[1].ShouldBe(new DiaDisponible(Hoy.AddDays(2), EstadoDia.Poca, 1, 4, null));
        disponibilidad.Dias[3].Motivo.ShouldBe("Día inhábil");
    }

    [Fact]
    public async Task RN03_un_rango_pedido_fuera_de_la_ventana_se_recorta()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync(diasVentana: 10);
        using var cliente = Fabrica.CrearCliente();

        var disponibilidad = await cliente.Http.GetFromJsonAsync<DisponibilidadOferta>(
            $"/api/publico/ofertas/{oferta.OfertaId}/disponibilidad?desde=2026-10-01&hasta=2027-12-31", ClienteTiie.Json);

        (disponibilidad!.Desde, disponibilidad.Hasta).ShouldBe((Hoy.AddDays(1), Hoy.AddDays(10)));
        disponibilidad.Dias.Count.ShouldBe(10);
    }

    [Fact]
    public async Task RF02_los_cupos_de_un_dia_son_las_horas_abiertas_con_lugar_y_hoy_esta_fuera_de_la_ventana()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        var jueves = new DateOnly(2026, 11, 5);
        var abierto = await CupoAsync(oferta.OfertaId, jueves, new TimeOnly(9, 0), capacidad: 3);
        var lleno = await CupoAsync(oferta.OfertaId, jueves, new TimeOnly(10, 0), capacidad: 1);
        await CupoAsync(oferta.OfertaId, jueves, new TimeOnly(11, 0), cerrado: true);
        await ConDbAsync(db => db.Cupos.Where(c => c.CupoId == lleno).ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)1)));
        using var cliente = Fabrica.CrearCliente();

        var cupos = await cliente.Http.GetFromJsonAsync<List<CupoDisponible>>(
            $"/api/publico/ofertas/{oferta.OfertaId}/cupos?fecha=2026-11-05", ClienteTiie.Json);
        var hoy = await cliente.GetAsync($"/api/publico/ofertas/{oferta.OfertaId}/cupos?fecha=2026-11-02");

        cupos.ShouldBe([new CupoDisponible(abierto, new TimeOnly(9, 0), 3)]);
        (await hoy.CodigoAsync()).ShouldBe(nameof(CodigoError.FueraDeVentana));
        (await cliente.Http.GetStringAsync($"/api/publico/ofertas/{oferta.OfertaId}/cupos?fecha=2026-11-05")).ShouldContain("\"hora\":\"09:00\"");
    }
}
