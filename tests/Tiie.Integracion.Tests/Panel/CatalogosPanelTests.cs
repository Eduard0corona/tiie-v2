using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Catalogos;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Panel;

/// <summary>Módulos, tipos de trámite y ofertas desde el panel (RF-12, RF-13, RN-11).</summary>
public sealed class CatalogosPanelTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static object Modulo(string nombre, string? version = null) => new
    {
        municipioId = 6,
        nombre,
        calle = "Av. Obregón",
        numeroExterior = "",
        colonia = "Centro",
        codigoPostal = "80000",
        telefono = "6671234567",
        horarioAtencion = "Lunes a viernes de 8:00 a 15:00",
        latitud = 24.8m,
        longitud = -107.39m,
        version,
    };

    [Fact]
    public async Task RF12_el_administrador_crea_y_edita_un_modulo_con_control_de_version()
    {
        await ModuloAsync();                                                   // siembra entidad y municipio
        using var admin = await ClienteComoAsync(Roles.Administrador);

        var creado = await (await admin.PostAsync("/api/admin/modulos", Modulo("Módulo Norte"))).LeerAsync<ModuloDetalle>();
        creado.NumeroExterior.ShouldBe("S/N");
        var editado = await (await admin.Http.PutAsJsonAsync($"/api/admin/modulos/{creado.ModuloId}", Modulo("Módulo Norte II", creado.Version), ClienteTiie.Json))
            .LeerAsync<ModuloDetalle>();
        var conVersionVieja = await admin.Http.PutAsJsonAsync($"/api/admin/modulos/{creado.ModuloId}", Modulo("Otro nombre", creado.Version), ClienteTiie.Json);

        editado.Nombre.ShouldBe("Módulo Norte II");
        editado.Version.ShouldNotBe(creado.Version);
        conVersionVieja.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conVersionVieja.CodigoAsync()).ShouldBe(nameof(CodigoError.Conflicto));
        (await ConDbAsync(db => db.Auditorias.CountAsync(a => a.Accion == "Modulo.Modificar"))).ShouldBe(1);
    }

    [Fact]
    public async Task RF12_un_codigo_postal_invalido_responde_Validacion()
    {
        await ModuloAsync();
        using var admin = await ClienteComoAsync(Roles.Administrador);

        var respuesta = await admin.PostAsync("/api/admin/modulos", new { municipioId = 6, nombre = "X", calle = "Y", colonia = "Z", codigoPostal = "800" });

        (await respuesta.ProblemaAsync()).GetProperty("errors").GetProperty("codigoPostal")[0].GetString().ShouldBe("Escribe 5 dígitos.");
    }

    [Fact]
    public async Task RN11_desactivar_un_modulo_no_borra_nada_y_avisa_cuantas_citas_futuras_tiene()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var oferta = await OfertaAsync();
        var cupo = await CupoAsync(oferta.OfertaId, new DateOnly(2026, 11, 5), new TimeOnly(10, 0));
        using (var publico = Fabrica.CrearCliente())
            await ReservarAsync(publico, Solicitud(cupo, GeneradorCurp.Valida(1)));
        using var admin = await ClienteComoAsync(Roles.Administrador);

        var resultado = await (await admin.PostAsync($"/api/admin/modulos/{oferta.ModuloId}/activacion", new { activo = false }))
            .LeerAsync<ResultadoActivacionModulo>();

        resultado.ShouldBe(new ResultadoActivacionModulo(false, 1));
        (await ConDbAsync(db => db.Modulos.CountAsync())).ShouldBe(1);
        using var publico2 = Fabrica.CrearCliente();
        (await publico2.GetAsync($"/api/publico/ofertas/{oferta.OfertaId}/disponibilidad")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Personal_del_modulo_A_solo_ve_sus_modulos_y_recibe_404_con_los_del_modulo_B()
    {
        var a = await ModuloAsync("Módulo A");
        var b = await ModuloAsync("Módulo B");
        using var capturista = await ClienteComoAsync(Roles.Capturista, a);

        var lista = await capturista.Http.GetFromJsonAsync<List<ModuloResumen>>("/api/admin/modulos", ClienteTiie.Json);

        lista!.Select(m => m.ModuloId).ShouldBe([a]);
        (await capturista.GetAsync($"/api/admin/modulos/{a}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await capturista.GetAsync($"/api/admin/modulos/{b}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RF13_un_requisito_de_5000_caracteres_se_guarda_completo_y_editar_reemplaza_la_lista()
    {
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var largo = string.Concat(Enumerable.Repeat("Constancia con fotografía y sello.\n", 200))[..5000];

        var creado = await (await admin.PostAsync("/api/admin/tipos-tramite", new
        {
            nombre = "Primera vez",
            descripcion = "Nunca ha tenido TIIE",
            costo = 0,
            notas = "Llega temprano",
            requisitos = new object[] { new { nivelEscolarId = (byte?)1, texto = largo }, new { nivelEscolarId = (byte?)null, texto = "CURP." } },
        })).LeerAsync<TipoTramiteDetalle>();
        var editado = await (await admin.Http.PutAsJsonAsync($"/api/admin/tipos-tramite/{creado.TipoTramiteId}", new
        {
            nombre = "Primera vez",
            costo = 50,
            requisitos = new object[] { new { nivelEscolarId = (byte?)2, texto = "Ficha de inscripción." } },
            activo = true,
            version = creado.Version,
        }, ClienteTiie.Json)).LeerAsync<TipoTramiteDetalle>();
        var duplicado = await admin.PostAsync("/api/admin/tipos-tramite", new { nombre = "Primera vez", requisitos = Array.Empty<object>() });

        creado.Requisitos[0].Texto.ShouldBe(largo);
        creado.Requisitos.Select(r => r.Orden).ShouldBe([(short)1, (short)2]);
        editado.Costo.ShouldBe(50m);
        editado.Requisitos.Select(r => r.Texto).ShouldBe(["Ficha de inscripción."]);
        (await duplicado.ProblemaAsync()).GetProperty("errors").GetProperty("nombre")[0].GetString().ShouldBe("Ya existe un tipo de trámite con ese nombre.");
    }

    [Fact]
    public async Task RF13_el_supervisor_crea_una_oferta_de_su_modulo_y_se_generan_sus_cupos()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var propio = await ModuloAsync("Propio");
        var ajeno = await ModuloAsync("Ajeno");
        var tipo = (await OfertaAsync(moduloId: ajeno)).TipoTramiteId;
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, propio);

        var creada = await supervisor.PostAsync("/api/admin/ofertas", new { moduloId = propio, tipoTramiteId = tipo, diasVentana = (short)14 });
        var enAjeno = await supervisor.PostAsync("/api/admin/ofertas", new { moduloId = ajeno, tipoTramiteId = tipo });
        var ventanaInvalida = await supervisor.PostAsync("/api/admin/ofertas", new { moduloId = propio, tipoTramiteId = tipo, diasVentana = (short)121 });
        var repetida = await supervisor.PostAsync("/api/admin/ofertas", new { moduloId = propio, tipoTramiteId = tipo });

        creada.StatusCode.ShouldBe(HttpStatusCode.Created);
        var oferta = await creada.LeerAsync<OfertaAdmin>();
        oferta.DiasVentana.ShouldBe((short)14);
        enAjeno.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ventanaInvalida.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
        (await repetida.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));

        // Sin plantilla aún no hay cupos; con plantilla, guardarla los genera.
        (await ConDbAsync(db => db.Cupos.CountAsync(c => c.OfertaId == oferta.OfertaId))).ShouldBe(0);
        var actualizada = await (await supervisor.Http.PutAsJsonAsync($"/api/admin/ofertas/{oferta.OfertaId}",
            new { diasVentana = (short)7, notasSede = "Entrada por la calle lateral", activo = true, version = oferta.Version }, ClienteTiie.Json))
            .LeerAsync<OfertaAdmin>();
        actualizada.NotasSede.ShouldBe("Entrada por la calle lateral");
    }
}
