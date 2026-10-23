using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Integracion.Tests.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Integracion.Tests.Autorizacion;

/// <summary>Políticas por endpoint, matriz de roles, MFA obligatoria y alcance por módulo (docs/07).</summary>
public sealed class AutorizacionTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private IReadOnlyList<(string Clave, Endpoint Endpoint)> EndpointsAdmin() =>
        [.. Fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => ("/" + e.RoutePattern.RawText!.TrimStart('/')).StartsWith("/api/admin/", StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => ($"{m} /{e.RoutePattern.RawText!.TrimStart('/')}", (Endpoint)e)))];

    private static string Politica(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return MatrizPermisos.Anonimo;
        var autorizacion = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        return autorizacion.Count > 0 ? autorizacion[^1].Policy ?? "(sin política)" : "(sin política)";
    }

    [Fact]
    public void Todo_endpoint_de_api_admin_esta_en_la_matriz_con_su_politica()
    {
        var reales = EndpointsAdmin().ToDictionary(e => e.Clave, e => Politica(e.Endpoint), StringComparer.Ordinal);

        reales.Keys.Order().ShouldBe(MatrizPermisos.Endpoints.Keys.Order(), "Declara el endpoint en MatrizPermisos.");
        foreach (var (clave, politica) in reales)
            politica.ShouldBe(MatrizPermisos.Endpoints[clave], clave);
    }

    private static HttpRequestMessage Solicitud(string clave)
    {
        var (metodo, ruta) = (clave.Split(' ')[0], MatrizPermisos.RutaConcreta(clave.Split(' ')[1]));
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), new Uri(ruta, UriKind.Relative));
        if (metodo != "GET")
            solicitud.Content = System.Net.Http.Json.JsonContent.Create(new { });
        return solicitud;
    }

    [Fact]
    public async Task Anonimo_recibe_401_NoAutenticado_en_cada_endpoint_protegido()
    {
        using var cliente = Fabrica.CrearCliente();

        foreach (var clave in MatrizPermisos.Endpoints.Where(e => e.Value != MatrizPermisos.Anonimo).Select(e => e.Key))
        {
            using var solicitud = Solicitud(clave);
            var respuesta = await cliente.Http.SendAsync(solicitud);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, clave);
            (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.NoAutenticado), clave);
        }
    }

    [Theory]
    [InlineData(Roles.Capturista)]
    [InlineData(Roles.Supervisor)]
    public async Task Cada_rol_recibe_403_Prohibido_en_las_rutas_que_su_politica_no_admite(string rol)
    {
        using var cliente = await ClienteComoAsync(rol);
        var prohibidas = MatrizPermisos.Endpoints
            .Where(e => e.Value != MatrizPermisos.Anonimo && !MatrizPermisos.RolesPorPolitica[e.Value].Contains(rol))
            .Select(e => e.Key)
            .ToList();
        prohibidas.ShouldNotBeEmpty();

        foreach (var clave in prohibidas)
        {
            using var solicitud = Solicitud(clave);
            var respuesta = await cliente.Http.SendAsync(solicitud);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden, clave);
            (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.Prohibido), clave);
        }
    }

    public static TheoryData<string, string> PoliticasYRoles()
    {
        var datos = new TheoryData<string, string>();
        foreach (var politica in MatrizPermisos.RolesPorPolitica.Keys)
            foreach (var rol in Roles.Todos)
                datos.Add(politica, rol);
        return datos;
    }

    [Theory]
    [MemberData(nameof(PoliticasYRoles))]
    public async Task Cada_politica_admite_exactamente_los_roles_de_la_matriz(string politica, string rol)
    {
        var autorizacion = Fabrica.Services.GetRequiredService<IAuthorizationService>();

        var resultado = await autorizacion.AuthorizeAsync(Principal(rol, mfa: true), politica);

        resultado.Succeeded.ShouldBe(MatrizPermisos.RolesPorPolitica[politica].Contains(rol));
    }

    [Theory]
    [InlineData(Politicas.Personal)]
    [InlineData(Politicas.Supervisor)]
    [InlineData(Politicas.Administrador)]
    public async Task Sin_MFA_ni_el_administrador_pasa_las_politicas_del_personal(string politica)
    {
        var autorizacion = Fabrica.Services.GetRequiredService<IAuthorizationService>();

        (await autorizacion.AuthorizeAsync(Principal(Roles.Administrador, mfa: false), politica)).Succeeded.ShouldBeFalse();
        (await autorizacion.AuthorizeAsync(Principal(Roles.Administrador, mfa: false), Politicas.PersonalSinMfa)).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Roles.Capturista)]
    [InlineData(Roles.Supervisor)]
    public async Task Capturista_y_supervisor_reciben_403_Prohibido_en_rutas_de_administrador(string rol)
    {
        using var cliente = await ClienteComoAsync(rol);

        var respuesta = await cliente.GetAsync("/api/admin/procesos");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.Prohibido));
    }

    [Fact]
    public async Task El_administrador_accede_a_sus_rutas()
    {
        using var cliente = await ClienteComoAsync(Roles.Administrador);

        (await cliente.GetAsync("/api/admin/procesos")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cliente.GetAsync("/api/admin/catalogos")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Una_sesion_sin_MFA_solo_accede_a_yo_mfa_y_salir()
    {
        var usuario = await UsuarioAsync(Roles.Administrador, conMfa: false);
        using var cliente = await ClienteDeAsync(usuario);

        (await cliente.GetAsync("/api/admin/auth/yo")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cliente.GetAsync("/api/admin/auth/mfa")).StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var bloqueada in new[] { "/api/admin/catalogos", "/api/admin/procesos" })
        {
            var respuesta = await cliente.GetAsync(bloqueada);
            respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden, bloqueada);
            (await respuesta.CodigoAsync()).ShouldBe(nameof(CodigoError.MfaRequerido), bloqueada);
        }
        var contrasena = await cliente.PostAsync("/api/admin/auth/contrasena", new { actual = Contrasena, nueva = "otra-contrasena-larga" });
        (await contrasena.CodigoAsync()).ShouldBe(nameof(CodigoError.MfaRequerido));
        (await cliente.PostAsync("/api/admin/auth/salir")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public void Personal_del_modulo_A_recibe_NoEncontrado_con_recursos_del_modulo_B()
    {
        var alcance = Alcance(Principal(Roles.Capturista, mfa: true, modulos: [1]));

        alcance.PuedeAcceder(1).ShouldBeTrue();
        alcance.PuedeAcceder(2).ShouldBeFalse();
        Should.Throw<NoEncontradoException>(() => alcance.ExigirAcceso(2));
    }

    [Fact]
    public void El_administrador_accede_a_todos_los_modulos_sin_asignarlos()
    {
        var alcance = Alcance(Principal(Roles.Administrador, mfa: true));

        alcance.ModulosPermitidos.ShouldBeNull();
        alcance.PuedeAcceder(999).ShouldBeTrue();
    }

    [Fact]
    public void Sin_sesion_no_hay_modulos_permitidos()
    {
        Alcance(new ClaimsPrincipal(new ClaimsIdentity())).ModulosPermitidos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Yo_devuelve_solo_los_modulos_asignados_al_capturista()
    {
        var propio = await ModuloAsync("Módulo propio");
        await ModuloAsync("Módulo ajeno");
        using var cliente = await ClienteComoAsync(Roles.Capturista, propio);

        var sesion = await (await cliente.GetAsync("/api/admin/auth/yo")).LeerAsync<SesionPersonal>();

        sesion.Rol.ShouldBe(Roles.Capturista);
        sesion.MfaActivo.ShouldBeTrue();
        sesion.Modulos.Select(m => m.Nombre).ShouldBe(["Módulo propio"]);
    }

    private static AutorizacionModulo Alcance(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

    private static ClaimsPrincipal Principal(string rol, bool mfa, int[]? modulos = null)
    {
        var identidad = new ClaimsIdentity(IdentityConstants.ApplicationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identidad.AddClaim(new Claim(ClaimTypes.NameIdentifier, "1"));
        identidad.AddClaim(new Claim(ClaimTypes.Role, rol));
        identidad.AddClaim(new Claim(ClaimsTiie.Mfa, mfa ? "true" : "false"));
        foreach (var modulo in modulos ?? [])
            identidad.AddClaim(new Claim(ClaimsTiie.Modulo, modulo.ToString(CultureInfo.InvariantCulture)));
        return new ClaimsPrincipal(identidad);
    }
}
