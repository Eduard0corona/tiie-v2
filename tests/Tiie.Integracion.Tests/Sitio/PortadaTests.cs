using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Seguridad;
using Tiie.Aplicacion.Sitio;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Sitio;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Sitio;

/// <summary>Contenido de la portada: lo lee el sitio público y lo edita el administrador (RF-01).</summary>
public sealed class PortadaTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static object Datos(
        string? version, string titulo = "Agenda tu credencial", string? aviso = null, DateOnly? desde = null, DateOnly? hasta = null,
        string respuesta = "Revisa la [sección de trámites](/#tramites).")
    {
        var preguntas = new[]
        {
            new { pregunta = "¿Cuánto cuesta?", respuesta },
            new { pregunta = "¿Qué llevo?", respuesta = "Tu CURP." },
        };
        return new
        {
            etiqueta = "Ciclo escolar 2026-2027",
            titulo,
            tituloDestacado = (string?)null,
            descripcion = "Elige sede, día y hora.",
            avisoTexto = aviso,
            avisoTono = TonoAviso.Importante,
            avisoDesde = desde,
            avisoHasta = hasta,
            preguntas,
            version,
        };
    }

    private static Task<HttpResponseMessage> GuardarAsync(ClienteTiie cliente, object datos) =>
        cliente.Http.PutAsJsonAsync(new Uri("/api/admin/portada", UriKind.Relative), datos, ClienteTiie.Json);

    private async Task<PortadaPublica> PublicaAsync()
    {
        using var publico = Fabrica.CrearCliente();
        return await (await publico.GetAsync("/api/publico/portada")).LeerAsync<PortadaPublica>();
    }

    [Fact]
    public async Task RF01_sin_contenido_guardado_la_portada_muestra_el_predeterminado()
    {
        var portada = await PublicaAsync();

        portada.TituloDestacado.ShouldBe("Tarjeta Inteligente de Identidad Estudiantil");
        portada.Aviso.ShouldBeNull();
        portada.Preguntas.Count.ShouldBe(8);
        portada.Preguntas.ShouldAllBe(p => ReglasPortada.EnlacesPermitidos(p.Respuesta));
    }

    [Fact]
    public async Task RF01_el_administrador_edita_la_portada_y_el_sitio_publico_la_muestra()
    {
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var inicial = await (await admin.GetAsync("/api/admin/portada")).LeerAsync<PortadaAdmin>();

        var guardada = await (await GuardarAsync(admin, Datos(inicial.Version))).LeerAsync<PortadaAdmin>();
        var editada = await (await GuardarAsync(admin, Datos(guardada.Version, titulo: "Agenda tu TIIE"))).LeerAsync<PortadaAdmin>();
        var portada = await PublicaAsync();

        inicial.Version.ShouldBeNull();                                        // aún es el predeterminado
        guardada.Version.ShouldNotBeNull();
        editada.Version.ShouldNotBe(guardada.Version);
        portada.Titulo.ShouldBe("Agenda tu TIIE");
        portada.TituloDestacado.ShouldBeNull();
        portada.Preguntas.Select(p => p.Pregunta).ShouldBe(["¿Cuánto cuesta?", "¿Qué llevo?"]);
        var registros = await ConDbAsync(db => db.Auditorias.AsNoTracking().Where(a => a.Accion == "Portada.Guardar").OrderBy(a => a.AuditoriaId).ToListAsync());
        registros.Count.ShouldBe(2);
        registros[1].Cambios!.ShouldContain("Agenda tu TIIE");
        registros[1].UsuarioId.ShouldNotBeNull();
    }

    [Fact]
    public async Task RF01_guardar_sobre_una_version_que_ya_cambio_responde_Conflicto()
    {
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var guardada = await (await GuardarAsync(admin, Datos(null))).LeerAsync<PortadaAdmin>();
        (await GuardarAsync(admin, Datos(guardada.Version, titulo: "Primero"))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var conVersionVieja = await GuardarAsync(admin, Datos(guardada.Version, titulo: "Segundo"));
        var comoSiNoExistiera = await GuardarAsync(admin, Datos(null, titulo: "Tercero"));

        conVersionVieja.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conVersionVieja.CodigoAsync()).ShouldBe(nameof(CodigoError.Conflicto));
        comoSiNoExistiera.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PublicaAsync()).Titulo.ShouldBe("Primero");
    }

    [Fact]
    public async Task RF01_el_aviso_solo_se_muestra_dentro_de_sus_fechas_en_la_hora_de_Sinaloa()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var guardada = await (await GuardarAsync(admin, Datos(null, aviso: "No habrá servicio el martes.", desde: Manana, hasta: Manana)))
            .LeerAsync<PortadaAdmin>();
        var hoy = await PublicaAsync();

        FijarReloj(Manana, new TimeOnly(7, 0));
        var manana = await PublicaAsync();

        FijarReloj(Manana.AddDays(1), new TimeOnly(0, 5));
        var despues = await PublicaAsync();
        using var adminDespues = await ClienteComoAsync(Roles.Administrador);       // la sesión anterior ya venció
        var estadoDespues = (await (await adminDespues.GetAsync("/api/admin/portada")).LeerAsync<PortadaAdmin>()).EstadoAviso;

        guardada.EstadoAviso.ShouldBe(EstadoAviso.Programado);
        hoy.Aviso.ShouldBeNull();
        manana.Aviso.ShouldBe(new AvisoPortada("No habrá servicio el martes.", TonoAviso.Importante));
        despues.Aviso.ShouldBeNull();
        estadoDespues.ShouldBe(EstadoAviso.Vencido);
    }

    [Fact]
    public async Task RF01_textos_vacios_enlaces_no_permitidos_o_fechas_al_reves_responden_Validacion()
    {
        using var admin = await ClienteComoAsync(Roles.Administrador);

        var respuesta = await GuardarAsync(admin, Datos(null, titulo: " ", aviso: "Aviso", desde: Manana, hasta: Hoy,
            respuesta: "Haz [clic](javascript:void0)."));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var errores = (await respuesta.ProblemaAsync()).GetProperty("errors");
        errores.GetProperty("titulo")[0].GetString().ShouldBe("Escribe el título.");
        errores.GetProperty("avisoHasta")[0].GetString().ShouldBe("La fecha final no puede ser anterior a la inicial.");
        errores.GetProperty("preguntas[0].respuesta")[0].GetString().ShouldBe("Los enlaces deben empezar con /, https://, mailto: o tel:.");
        (await ConDbAsync(db => db.ContenidosPortada.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task RF01_solo_el_administrador_edita_la_portada()
    {
        var moduloId = await ModuloAsync();
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, moduloId);
        using var anonimo = Fabrica.CrearCliente();

        (await supervisor.GetAsync("/api/admin/portada")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GuardarAsync(supervisor, Datos(null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GuardarAsync(anonimo, Datos(null))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
