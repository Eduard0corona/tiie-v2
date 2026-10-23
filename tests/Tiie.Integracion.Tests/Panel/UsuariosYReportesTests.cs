using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Reportes;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Infraestructura.Identidad;
using Tiie.Integracion.Tests.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Integracion.Tests.Panel;

/// <summary>Usuarios del personal (RF-16).</summary>
public sealed partial class UsuariosPanelTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static object Nuevo(string usuario, string rol, params int[] modulos) => new
    {
        usuario,
        correo = $"{usuario}@tiie.test",
        nombre = "Nora",
        apellidoPaterno = "Ruiz",
        rol,
        moduloIds = modulos,
    };

    [Fact]
    public async Task RF16_el_administrador_crea_un_usuario_que_activa_su_cuenta_con_la_invitacion_y_configura_MFA()
    {
        var modulo = await ModuloAsync();
        using var admin = await ClienteComoAsync(Roles.Administrador);

        var creado = await admin.PostAsync("/api/admin/usuarios", Nuevo("nora.ruiz", Roles.Capturista, modulo));

        creado.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detalle = await creado.LeerAsync<UsuarioDetalle>();
        detalle.CuentaActivada.ShouldBeFalse();
        detalle.ModuloIds.ShouldBe([modulo]);
        var invitacion = Fabrica.Correos.Enviados.Single();
        invitacion.Destinatario.ShouldBe("nora.ruiz@tiie.test");
        var enlace = Enlace().Match(invitacion.Texto);
        enlace.Groups["base"].Value.ShouldBe($"{FabricaTiie.UrlPublica}/admin/activar");

        using var nora = Fabrica.CrearCliente();
        (await nora.PostAsync("/api/admin/auth/activacion", new
        {
            usuarioId = int.Parse(enlace.Groups["id"].Value, CultureInfo.InvariantCulture),
            token = enlace.Groups["token"].Value,
            contrasena = "contrasena-de-nora-larga",
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await nora.EntrarAsync("nora.ruiz", "contrasena-de-nora-larga")).LeerAsync<RespuestaEntrar>()).SiguientePaso
            .ShouldBe(PasoSiguiente.ConfigurarMfa);
    }

    [Fact]
    public async Task RF16_validaciones_de_alta_usuario_repetido_y_capturista_sin_modulos()
    {
        var modulo = await ModuloAsync();
        using var admin = await ClienteComoAsync(Roles.Administrador);
        await admin.PostAsync("/api/admin/usuarios", Nuevo("repetido", Roles.Capturista, modulo));

        var repetido = await admin.PostAsync("/api/admin/usuarios", Nuevo("repetido", Roles.Capturista, modulo));
        var sinModulos = await admin.PostAsync("/api/admin/usuarios", Nuevo("sin.modulos", Roles.Supervisor));
        var rolInventado = await admin.PostAsync("/api/admin/usuarios", Nuevo("inventado", "Director", modulo));

        (await repetido.ProblemaAsync()).GetProperty("errors").TryGetProperty("usuario", out _).ShouldBeTrue();
        (await sinModulos.ProblemaAsync()).GetProperty("errors").TryGetProperty("moduloIds", out _).ShouldBeTrue();
        (await rolInventado.ProblemaAsync()).GetProperty("errors").TryGetProperty("rol", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task RF16_si_la_invitacion_no_sale_responde_503_y_la_cuenta_queda_creada()
    {
        var modulo = await ModuloAsync();
        using var admin = await ClienteComoAsync(Roles.Administrador);
        Fabrica.Correos.Fallar = true;

        var respuesta = await admin.PostAsync("/api/admin/usuarios", Nuevo("sin.correo", Roles.Capturista, modulo));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problema = await respuesta.ProblemaAsync();
        problema.GetProperty("codigo").GetString().ShouldBe(nameof(CodigoError.CorreoNoDisponible));
        var id = await ConDbAsync(db => db.Users.Where(u => u.UserName == "sin.correo").Select(u => u.Id).SingleAsync());
        problema.GetProperty("usuarioId").GetInt32().ShouldBe(id);
        Fabrica.Correos.Fallar = false;
        (await admin.PostAsync($"/api/admin/usuarios/{id}/invitacion")).StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task RF16_desactivar_un_usuario_cierra_sus_sesiones_y_nadie_se_desactiva_a_si_mismo()
    {
        var modulo = await ModuloAsync();
        var adminUsuario = await UsuarioAsync(Roles.Administrador);
        using var admin = await ClienteDeAsync(adminUsuario);
        var capturistaUsuario = await UsuarioAsync(Roles.Capturista, modulos: modulo);
        using var capturista = await ClienteDeAsync(capturistaUsuario);

        (await admin.PostAsync($"/api/admin/usuarios/{capturistaUsuario.Id}/activacion", new { activo = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        (await capturista.GetAsync("/api/admin/auth/yo")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var adminDeNuevo = await ClienteDeAsync(adminUsuario);
        var aSiMismo = await adminDeNuevo.PostAsync($"/api/admin/usuarios/{adminUsuario.Id}/activacion", new { activo = false });
        (await aSiMismo.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
    }

    [Fact]
    public async Task RF16_siempre_queda_al_menos_un_administrador_activo()
    {
        var unico = await UsuarioAsync(Roles.Administrador);
        var otro = await UsuarioAsync(Roles.Administrador);
        using var cliente = await ClienteDeAsync(unico);
        (await cliente.PostAsync($"/api/admin/usuarios/{otro.Id}/activacion", new { activo = false })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var detalle = await cliente.Http.GetFromJsonAsync<UsuarioDetalle>($"/api/admin/usuarios/{unico.Id}", ClienteTiie.Json);
        var quitarseElRol = await cliente.Http.PutAsJsonAsync($"/api/admin/usuarios/{unico.Id}", new
        {
            correo = detalle!.Correo,
            nombre = detalle.Nombre,
            apellidoPaterno = detalle.ApellidoPaterno,
            rol = Roles.Supervisor,
            moduloIds = Array.Empty<int>(),
            version = detalle.Version,
        }, ClienteTiie.Json);

        (await quitarseElRol.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
    }

    [Fact]
    public async Task RF16_restablecer_el_MFA_obliga_a_configurarlo_de_nuevo()
    {
        var modulo = await ModuloAsync();
        var capturista = await UsuarioAsync(Roles.Capturista, modulos: modulo);
        using var admin = await ClienteComoAsync(Roles.Administrador);

        (await admin.PostAsync($"/api/admin/usuarios/{capturista.Id}/mfa/restablecimiento")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var cliente = Fabrica.CrearCliente();
        (await (await cliente.EntrarAsync(capturista.Usuario, capturista.Contrasena)).LeerAsync<RespuestaEntrar>()).SiguientePaso
            .ShouldBe(PasoSiguiente.ConfigurarMfa);
    }

    [Fact]
    public async Task RF16_editar_con_una_version_vieja_responde_Conflicto()
    {
        var modulo = await ModuloAsync();
        var capturista = await UsuarioAsync(Roles.Capturista, modulos: modulo);
        using var admin = await ClienteComoAsync(Roles.Administrador);
        var detalle = await admin.Http.GetFromJsonAsync<UsuarioDetalle>($"/api/admin/usuarios/{capturista.Id}", ClienteTiie.Json);
        object Cuerpo(string nombre) => new
        {
            correo = detalle!.Correo,
            nombre,
            apellidoPaterno = detalle.ApellidoPaterno,
            rol = Roles.Supervisor,
            moduloIds = new[] { modulo },
            version = detalle.Version,
        };

        var primera = await admin.Http.PutAsJsonAsync($"/api/admin/usuarios/{capturista.Id}", Cuerpo("Nuevo"), ClienteTiie.Json);
        var segunda = await admin.Http.PutAsJsonAsync($"/api/admin/usuarios/{capturista.Id}", Cuerpo("Otro"), ClienteTiie.Json);

        (await primera.LeerAsync<UsuarioDetalle>()).Rol.ShouldBe(Roles.Supervisor);
        (await segunda.CodigoAsync()).ShouldBe(nameof(CodigoError.Conflicto));
    }

    [GeneratedRegex(@"(?<base>https://[^\s?]+)\?usuarioId=(?<id>\d+)&token=(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex Enlace();
}

/// <summary>Reportes, tablero y bitácora (RF-17 a RF-19).</summary>
public sealed class ReportesPanelTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    [Fact]
    public async Task RF17_el_reporte_del_1_al_31_incluye_las_citas_del_dia_31_y_exporta_las_mismas_filas()
    {
        FijarReloj(new DateOnly(2026, 12, 1), new TimeOnly(9, 0));
        var oferta = await OfertaAsync();
        foreach (var (dia, n) in new[] { (1, 1), (31, 2) })
        {
            var cupo = await CupoAsync(oferta.OfertaId, new DateOnly(2026, 12, dia).AddDays(dia == 1 ? 1 : 0), new TimeOnly(10, 0));
            using var publico = Fabrica.CrearCliente();
            await ReservarAsync(publico, Solicitud(cupo, GeneradorCurp.Valida(n)));
        }
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, oferta.ModuloId);

        var reporte = await supervisor.Http.GetFromJsonAsync<ReporteCitas>("/api/admin/reportes/citas?desde=2026-12-01&hasta=2026-12-31", ClienteTiie.Json);
        var excel = await supervisor.GetAsync("/api/admin/reportes/citas?desde=2026-12-01&hasta=2026-12-31&formato=xlsx");
        var pdf = await supervisor.GetAsync("/api/admin/reportes/citas?desde=2026-12-01&hasta=2026-12-31&formato=pdf");

        reporte!.Filas.Select(f => f.Fecha).ShouldBe([new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 31)]);
        reporte.Totales.Single().ShouldBe(new TotalEstado(EstadoCita.Agendada, 2));
        excel.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await excel.Content.ReadAsByteArrayAsync())[..2].ShouldBe("PK"u8.ToArray());
        System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..5]).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task RF17_el_supervisor_solo_ve_sus_modulos_y_el_capturista_no_tiene_reportes()
    {
        FijarReloj(Hoy, new TimeOnly(9, 0));
        var propia = await OfertaAsync();
        var ajena = await OfertaAsync();
        foreach (var (oferta, n) in new[] { (propia, 1), (ajena, 2) })
        {
            var cupo = await CupoAsync(oferta.OfertaId, new DateOnly(2026, 11, 5), new TimeOnly(10, 0));
            using var publico = Fabrica.CrearCliente();
            await ReservarAsync(publico, Solicitud(cupo, GeneradorCurp.Valida(n)));
        }
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, propia.ModuloId);
        using var capturista = await ClienteComoAsync(Roles.Capturista, propia.ModuloId);

        var reporte = await supervisor.Http.GetFromJsonAsync<ReporteCitas>("/api/admin/reportes/citas?desde=2026-11-01&hasta=2026-11-30", ClienteTiie.Json);
        var formatoInvalido = await supervisor.GetAsync("/api/admin/reportes/citas?desde=2026-11-01&hasta=2026-11-30&formato=csv");

        reporte!.Filas.Count.ShouldBe(1);
        (await formatoInvalido.CodigoAsync()).ShouldBe(nameof(CodigoError.Validacion));
        (await capturista.GetAsync("/api/admin/reportes/citas?desde=2026-11-01&hasta=2026-11-30")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await supervisor.GetAsync($"/api/admin/reportes/citas?desde=2026-11-01&hasta=2026-11-30&moduloId={ajena.ModuloId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RF18_el_tablero_muestra_hoy_por_estado_la_ocupacion_y_la_inasistencia()
    {
        FijarReloj(Hoy.AddDays(-3), new TimeOnly(9, 0));
        var oferta = await OfertaAsync();
        var pasado = await CupoAsync(oferta.OfertaId, Hoy.AddDays(-1), new TimeOnly(10, 0), capacidad: 2);
        var hoy = await CupoAsync(oferta.OfertaId, Hoy, new TimeOnly(10, 0));
        await CupoAsync(oferta.OfertaId, Hoy.AddDays(2), new TimeOnly(10, 0), capacidad: 4);
        using (var publico = Fabrica.CrearCliente())
        {
            await ReservarAsync(publico, Solicitud(pasado, GeneradorCurp.Valida(1)));
            await ReservarAsync(publico, Solicitud(pasado, GeneradorCurp.Valida(2)));
            await ReservarAsync(publico, Solicitud(hoy, GeneradorCurp.Valida(3)));
        }
        await ConDbAsync(async db =>
        {
            var ids = await db.Citas.Where(c => c.CupoId == pasado).OrderBy(c => c.CitaId).Select(c => c.CitaId).ToListAsync();
            await db.Citas.Where(c => c.CitaId == ids[0]).ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.Atendida));
            await db.Citas.Where(c => c.CitaId == ids[1]).ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.NoAsistio));
        });
        FijarReloj(Hoy, new TimeOnly(9, 0));
        using var capturista = await ClienteComoAsync(Roles.Capturista, oferta.ModuloId);

        var tablero = await capturista.Http.GetFromJsonAsync<Tablero>("/api/admin/tablero", ClienteTiie.Json);

        tablero!.Hoy.ShouldBe([new TotalEstado(EstadoCita.Agendada, 1)]);
        tablero.Proximos7Dias.ShouldBe([new DiaOcupacion(Hoy.AddDays(2), 4, 0)]);
        tablero.Inasistencia30Dias.Single().Porcentaje.ShouldBe(50.0);
    }

    [Fact]
    public async Task RF19_la_bitacora_registra_sesiones_y_cambios_y_solo_el_administrador_la_consulta()
    {
        var modulo = await ModuloAsync();
        using var admin = await ClienteComoAsync(Roles.Administrador);
        await admin.PostAsync($"/api/admin/modulos/{modulo}/activacion", new { activo = false });
        using var supervisor = await ClienteComoAsync(Roles.Supervisor, modulo);

        var pagina = await admin.Http.GetFromJsonAsync<ListaPaginada<RegistroBitacora>>("/api/admin/auditoria?tamano=100", ClienteTiie.Json);
        var soloModulos = await admin.Http.GetFromJsonAsync<ListaPaginada<RegistroBitacora>>("/api/admin/auditoria?entidad=Modulo", ClienteTiie.Json);

        pagina!.Elementos.Select(r => r.Accion).ShouldContain(AccionesAuditoria.SesionIniciar);
        soloModulos!.Elementos.Single().Accion.ShouldBe("Modulo.Modificar");
        soloModulos.Elementos.Single().Usuario.ShouldNotBeNull();
        (await supervisor.GetAsync("/api/admin/auditoria")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
