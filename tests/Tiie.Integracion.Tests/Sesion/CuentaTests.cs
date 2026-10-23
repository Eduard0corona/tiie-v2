using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Infraestructura.Identidad;
using Tiie.Integracion.Tests.Infraestructura;
using Tiie.Web.Seguridad;

namespace Tiie.Integracion.Tests.Sesion;

/// <summary>Activación por invitación, restablecimiento y cambio de contraseña (docs/07#activación-y-restablecimiento).</summary>
public sealed partial class CuentaTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private const string Nueva = "frase-nueva-para-la-cuenta";

    [Fact]
    public async Task Activar_la_cuenta_define_la_contrasena_confirma_el_correo_y_no_inicia_sesion()
    {
        var usuario = await UsuarioAsync(Roles.Capturista, conMfa: false, correoConfirmado: false);
        var token = await TokenAsync(usuario);
        using var cliente = Fabrica.CrearCliente();

        var activacion = await cliente.PostAsync("/api/admin/auth/activacion", new { usuarioId = usuario.Id, token, contrasena = Nueva });

        activacion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cliente.GetAsync("/api/admin/auth/yo")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var entrar = await cliente.EntrarAsync(usuario.Usuario, Nueva);
        (await entrar.LeerAsync<RespuestaEntrar>()).SiguientePaso.ShouldBe(PasoSiguiente.ConfigurarMfa);
    }

    [Fact]
    public async Task Sin_activar_la_cuenta_no_se_puede_entrar()
    {
        var usuario = await UsuarioAsync(Roles.Capturista, conMfa: false, correoConfirmado: false);
        using var cliente = Fabrica.CrearCliente();

        (await (await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena)).CodigoAsync())
            .ShouldBe(nameof(CodigoError.CredencialesInvalidas));
    }

    [Fact]
    public async Task El_enlace_de_activacion_alterado_o_ya_usado_responde_TokenInvalido()
    {
        var usuario = await UsuarioAsync(Roles.Capturista, conMfa: false, correoConfirmado: false);
        var token = await TokenAsync(usuario);
        using var cliente = Fabrica.CrearCliente();

        var alterado = await cliente.PostAsync("/api/admin/auth/activacion", new { usuarioId = usuario.Id, token = token[..^4] + "AAAA", contrasena = Nueva });
        (await alterado.CodigoAsync()).ShouldBe(nameof(CodigoError.TokenInvalido));

        (await cliente.PostAsync("/api/admin/auth/activacion", new { usuarioId = usuario.Id, token, contrasena = Nueva }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var repetido = await cliente.PostAsync("/api/admin/auth/activacion", new { usuarioId = usuario.Id, token, contrasena = Nueva });
        (await repetido.CodigoAsync()).ShouldBe(nameof(CodigoError.TokenInvalido));
    }

    [Theory]
    [InlineData("corta", "La contraseña debe tener al menos 12 caracteres.")]
    [InlineData("passwordpassword", "Esa contraseña es muy común o contiene tu usuario. Elige otra.")]
    [InlineData("123456789012", "Esa contraseña es muy común o contiene tu usuario. Elige otra.")]
    public async Task Una_contrasena_corta_o_comun_se_rechaza_con_Validacion(string contrasena, string mensaje)
    {
        var usuario = await UsuarioAsync(Roles.Capturista, conMfa: false, correoConfirmado: false);
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/api/admin/auth/activacion",
            new { usuarioId = usuario.Id, token = await TokenAsync(usuario), contrasena });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var errores = (await respuesta.ProblemaAsync()).GetProperty("errors").GetProperty("contrasena");
        errores.EnumerateArray().Select(e => e.GetString()).ShouldContain(mensaje);
    }

    [Fact]
    public async Task Olvide_mi_contrasena_responde_202_exista_o_no_el_correo_y_solo_escribe_al_registrado()
    {
        var usuario = await UsuarioAsync(Roles.Supervisor);
        using var cliente = Fabrica.CrearCliente();

        var existente = await cliente.PostAsync("/api/admin/auth/olvide-contrasena", new { correo = usuario.Correo });
        var inexistente = await cliente.PostAsync("/api/admin/auth/olvide-contrasena", new { correo = "nadie@tiie.test" });

        existente.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        inexistente.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await existente.Content.ReadAsStringAsync()).ShouldBe(await inexistente.Content.ReadAsStringAsync());
        Fabrica.Correos.Enviados.Select(c => c.Destinatario).ShouldBe([usuario.Correo]);
    }

    [Fact]
    public async Task Restablecer_con_el_enlace_del_correo_cambia_la_contrasena_y_desbloquea()
    {
        var usuario = await UsuarioAsync(Roles.Supervisor);
        using var cliente = Fabrica.CrearCliente();
        for (var i = 0; i < 5; i++)
            await cliente.EntrarAsync(usuario.Usuario, "no-es-la-contrasena");
        await cliente.PostAsync("/api/admin/auth/olvide-contrasena", new { correo = usuario.Correo });

        var correo = Fabrica.Correos.Enviados.Single();
        var enlace = Enlace().Match(correo.Texto);
        enlace.Success.ShouldBeTrue(correo.Texto);
        enlace.Groups["base"].Value.ShouldBe($"{FabricaTiie.UrlPublica}/admin/restablecer");
        correo.Html.ShouldContain(WebUtilityEncode(enlace.Value));

        var restablecer = await cliente.PostAsync("/api/admin/auth/restablecimiento", new
        {
            usuarioId = int.Parse(enlace.Groups["id"].Value, CultureInfo.InvariantCulture),
            token = enlace.Groups["token"].Value,
            contrasena = Nueva,
        });

        restablecer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await cliente.EntrarAsync(usuario.Usuario, Nueva)).LeerAsync<RespuestaEntrar>())
            .SiguientePaso.ShouldBe(PasoSiguiente.CodigoMfa);
    }

    [Fact]
    public async Task Cambiar_la_contrasena_exige_la_actual_y_conserva_la_sesion()
    {
        var usuario = await UsuarioAsync(Roles.Capturista);
        using var cliente = await ClienteDeAsync(usuario);

        var sinActual = await cliente.PostAsync("/api/admin/auth/contrasena", new { actual = "no-es-la-actual", nueva = Nueva });
        sinActual.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await sinActual.ProblemaAsync()).GetProperty("errors").TryGetProperty("actual", out _).ShouldBeTrue();

        (await cliente.PostAsync("/api/admin/auth/contrasena", new { actual = usuario.Contrasena, nueva = Nueva }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(6));
        (await cliente.GetAsync("/api/admin/catalogos")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Los_logs_de_la_cuenta_no_contienen_el_correo_ni_la_contrasena()
    {
        var usuario = await UsuarioAsync(Roles.Supervisor);
        using var cliente = await ClienteDeAsync(usuario);
        await cliente.PostAsync("/api/admin/auth/contrasena", new { actual = usuario.Contrasena, nueva = Nueva });
        await cliente.PostAsync("/api/admin/auth/olvide-contrasena", new { correo = usuario.Correo });
        await cliente.EntrarAsync(usuario.Usuario, "no-es-la-contrasena");

        Fabrica.Logs.Mensajes.ShouldNotBeEmpty();
        Fabrica.Logs.Mensajes.ShouldAllBe(m => !m.Contains(usuario.Correo, StringComparison.OrdinalIgnoreCase)
            && !m.Contains(Nueva, StringComparison.Ordinal)
            && !m.Contains(usuario.Contrasena, StringComparison.Ordinal));
    }

    private async Task<string> TokenAsync(UsuarioPrueba usuario)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var entidad = await usuarios.FindByIdAsync(usuario.Id.ToString(CultureInfo.InvariantCulture));
        return TokenEnlace.Codificar(await usuarios.GeneratePasswordResetTokenAsync(entidad!));
    }

    private static string WebUtilityEncode(string texto) => System.Net.WebUtility.HtmlEncode(texto);

    [GeneratedRegex(@"(?<base>https://[^\s?]+)\?usuarioId=(?<id>\d+)&token=(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex Enlace();
}
