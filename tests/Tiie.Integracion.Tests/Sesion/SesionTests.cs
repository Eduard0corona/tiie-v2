using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Identidad;
using Tiie.Integracion.Tests.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Integracion.Tests.Sesion;

/// <summary>Inicio de sesión del personal con contraseña y MFA (docs/07#inicio-de-sesión).</summary>
public sealed class SesionTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private const string Yo = "/api/admin/auth/yo";

    private string Codigo(UsuarioPrueba usuario) => Totp.Generar(usuario.ClaveMfa!, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Sin_MFA_configurada_entrar_pide_configurarla()
    {
        var usuario = await UsuarioAsync(Roles.Capturista, conMfa: false);
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);

        (await respuesta.LeerAsync<RespuestaEntrar>()).SiguientePaso.ShouldBe(PasoSiguiente.ConfigurarMfa);
        (await (await cliente.GetAsync(Yo)).LeerAsync<SesionPersonal>()).MfaActivo.ShouldBeFalse();
    }

    [Fact]
    public async Task Configurar_MFA_entrega_10_codigos_de_recuperacion_y_activa_la_sesion_completa()
    {
        var usuario = await UsuarioAsync(Roles.Capturista, conMfa: false);
        using var cliente = await ClienteDeAsync(usuario);

        var clave = await (await cliente.GetAsync("/api/admin/auth/mfa")).LeerAsync<ClaveAutenticador>();
        Uri.UnescapeDataString(clave.UriAutenticador).ShouldStartWith($"otpauth://totp/TIIE:{usuario.Correo}?secret=");
        clave.UriAutenticador.ShouldContain("&issuer=TIIE&digits=6");

        var incorrecto = await cliente.PostAsync("/api/admin/auth/mfa", new { codigo = "000000" });
        incorrecto.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await incorrecto.ProblemaAsync()).GetProperty("errors").GetProperty("codigo").GetArrayLength().ShouldBe(1);

        var codigo = Totp.Generar(clave.ClaveCompartida, DateTimeOffset.UtcNow);
        var activada = await (await cliente.PostAsync("/api/admin/auth/mfa", new { codigo = $"{codigo[..3]} {codigo[3..]}" }))
            .LeerAsync<RespuestaMfaActivada>();

        activada.CodigosRecuperacion.Count.ShouldBe(10);
        (await cliente.GetAsync("/api/admin/catalogos")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await cliente.GetAsync(Yo)).LeerAsync<SesionPersonal>()).MfaActivo.ShouldBeTrue();
    }

    [Fact]
    public async Task Con_MFA_activa_la_contrasena_sola_no_inicia_sesion()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);

        (await respuesta.LeerAsync<RespuestaEntrar>()).SiguientePaso.ShouldBe(PasoSiguiente.CodigoMfa);
        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var codigo = await cliente.PostAsync("/api/admin/auth/entrar/mfa", new { codigo = Codigo(usuario), recordarEsteEquipo = false });
        codigo.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_codigo_MFA_incorrecto_responde_CodigoMfaInvalido()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = Fabrica.CrearCliente();
        await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);

        var respuesta = await cliente.PostAsync("/api/admin/auth/entrar/mfa", new { codigo = "000000", recordarEsteEquipo = false });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.CodigoMfaInvalido));
    }

    [Fact]
    public async Task El_codigo_MFA_sin_haber_escrito_la_contrasena_responde_NoAutenticado()
    {
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/api/admin/auth/entrar/mfa", new { codigo = "123456", recordarEsteEquipo = false });

        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.NoAutenticado));
    }

    [Fact]
    public async Task Un_codigo_de_recuperacion_inicia_sesion_una_sola_vez()
    {
        var usuario = await UsuarioAsync(Roles.Supervisor);
        string recuperacion;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var entidad = await usuarios.FindByIdAsync(usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            recuperacion = (await usuarios.GenerateNewTwoFactorRecoveryCodesAsync(entidad!, 10))!.First();
        }

        using var primero = Fabrica.CrearCliente();
        await primero.EntrarAsync(usuario.Usuario, usuario.Contrasena);
        (await primero.PostAsync("/api/admin/auth/entrar/recuperacion", new { codigoRecuperacion = recuperacion.ToLowerInvariant() }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await primero.GetAsync("/api/admin/catalogos")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var segundo = Fabrica.CrearCliente();
        await segundo.EntrarAsync(usuario.Usuario, usuario.Contrasena);
        var repetido = await segundo.PostAsync("/api/admin/auth/entrar/recuperacion", new { codigoRecuperacion = recuperacion });
        (await repetido.CodigoAsync()).ShouldBe(nameof(CodigoError.CodigoMfaInvalido));
    }

    [Fact]
    public async Task Recordar_este_equipo_omite_el_codigo_en_el_siguiente_inicio()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = Fabrica.CrearCliente();
        await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);
        await cliente.PostAsync("/api/admin/auth/entrar/mfa", new { codigo = Codigo(usuario), recordarEsteEquipo = true });
        await cliente.PostAsync("/api/admin/auth/salir");

        var otraVez = await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);

        (await otraVez.LeerAsync<RespuestaEntrar>()).SiguientePaso.ShouldBe(PasoSiguiente.Listo);
        (await cliente.GetAsync("/api/admin/catalogos")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Una_contrasena_incorrecta_responde_CredencialesInvalidas_igual_que_un_usuario_inexistente()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = Fabrica.CrearCliente();

        var incorrecta = await cliente.EntrarAsync(usuario.Usuario, "no-es-la-contrasena");
        var inexistente = await cliente.EntrarAsync("nadie-se-llama-asi", "no-es-la-contrasena");

        incorrecta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await incorrecta.CodigoAsync()).ShouldBe(nameof(CodigoError.CredencialesInvalidas));
        (await inexistente.CodigoAsync()).ShouldBe(nameof(CodigoError.CredencialesInvalidas));
    }

    [Fact]
    public async Task Cinco_intentos_fallidos_bloquean_la_cuenta_15_minutos()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = Fabrica.CrearCliente();
        for (var i = 0; i < 4; i++)
            (await (await cliente.EntrarAsync(usuario.Usuario, "no-es-la-contrasena")).CodigoAsync())
                .ShouldBe(nameof(CodigoError.CredencialesInvalidas));

        var quinto = await cliente.EntrarAsync(usuario.Usuario, "no-es-la-contrasena");
        var conLaCorrecta = await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);

        quinto.StatusCode.ShouldBe((HttpStatusCode)StatusCodes.Status423Locked);
        var desbloqueo = (await quinto.ProblemaAsync()).GetProperty("desbloqueoEn").GetDateTimeOffset();
        (desbloqueo - DateTimeOffset.UtcNow).ShouldBeInRange(TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16));
        (await conLaCorrecta.CodigoAsync()).ShouldBe(nameof(CodigoError.CuentaBloqueada));
    }

    [Fact]
    public async Task Un_usuario_inactivo_no_puede_entrar()
    {
        var usuario = await UsuarioAsync(Roles.Administrador, activo: false);
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);

        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.CredencialesInvalidas));
    }

    [Fact]
    public async Task Desactivar_un_usuario_cierra_su_sesion_en_menos_de_5_minutos()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = await ClienteDeAsync(usuario);
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var entidad = (await usuarios.FindByIdAsync(usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)))!;
            entidad.Activo = false;
            await usuarios.UpdateAsync(entidad);
            await usuarios.UpdateSecurityStampAsync(entidad);
        }

        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task La_sesion_vence_a_los_30_minutos_sin_actividad()
    {
        using var cliente = await ClienteComoAsync(Roles.Capturista);

        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(31));

        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task La_sesion_vence_a_las_10_horas_aunque_siga_activa()
    {
        using var cliente = await ClienteComoAsync(Roles.Capturista);

        // Una petición cada 20 minutos renueva la cookie por inactividad durante 9 horas 40 minutos.
        for (var i = 0; i < 29; i++)
        {
            Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(20));
            (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.OK, $"a los {(i + 1) * 20} minutos");
        }
        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(21));

        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Salir_cierra_la_sesion()
    {
        using var cliente = await ClienteComoAsync(Roles.Capturista);

        (await cliente.PostAsync("/api/admin/auth/salir")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await cliente.GetAsync(Yo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Los_inicios_y_los_intentos_fallidos_quedan_en_la_auditoria()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using (var fallido = Fabrica.CrearCliente())
            await fallido.EntrarAsync(usuario.Usuario, "no-es-la-contrasena");
        using (var cliente = await ClienteDeAsync(usuario))
            await cliente.PostAsync("/api/admin/auth/salir");

        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var acciones = await alcance.ServiceProvider.GetRequiredService<TiieDbContext>().Auditorias
            .Where(a => a.UsuarioId == usuario.Id)
            .OrderBy(a => a.AuditoriaId)
            .Select(a => a.Accion)
            .ToListAsync();

        acciones.ShouldBe([AccionesAuditoria.SesionIntentoFallido, AccionesAuditoria.SesionIniciar, AccionesAuditoria.SesionCerrar]);
    }

    [Fact]
    public async Task Entrar_registra_el_ultimo_acceso_con_el_reloj_del_sitio()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = await ClienteDeAsync(usuario);

        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var acceso = await alcance.ServiceProvider.GetRequiredService<TiieDbContext>().Users
            .Where(u => u.Id == usuario.Id).Select(u => u.UltimoAccesoEn).SingleAsync();

        acceso.ShouldNotBeNull();
        (acceso.Value - Fabrica.Tiempo.GetUtcNow()).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }
}
