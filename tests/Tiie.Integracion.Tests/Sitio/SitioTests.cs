using System.Net;
using System.Net.Http.Json;
using System.Text;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Sitio;

/// <summary>Cabeceras de seguridad, CSRF y respuestas de error comunes (docs/11 y docs/07#protección-csrf).</summary>
public sealed class SitioTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    [Fact]
    public async Task Las_respuestas_llevan_CSP_HSTS_nosniff_y_Referrer_Policy()
    {
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/api/publico/configuracion");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cabeceras = respuesta.Headers;
        cabeceras.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
        cabeceras.GetValues("Content-Security-Policy").Single().ShouldNotContain("script-src 'self' 'unsafe-inline'");
        cabeceras.GetValues("Strict-Transport-Security").Single().ShouldContain("max-age=");
        cabeceras.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        cabeceras.GetValues("Referrer-Policy").Single().ShouldBe("strict-origin-when-cross-origin");
        cabeceras.GetValues("Permissions-Policy").Single().ShouldContain("camera=()");
    }

    [Fact]
    public async Task La_ruta_de_mi_cita_no_envia_referer()
    {
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/mi-cita/un-token-cualquiera");

        respuesta.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
    }

    [Fact]
    public async Task Un_POST_sin_X_Tiie_Csrf_es_rechazado()
    {
        using var cliente = Fabrica.CrearCliente();
        cliente.Http.DefaultRequestHeaders.Remove("X-Tiie-Csrf");

        var respuesta = await cliente.EntrarAsync("alguien", "una-contrasena-cualquiera");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await respuesta.CodigoAsync()).ShouldBe("SolicitudInvalida");
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    public async Task Un_POST_de_otro_sitio_segun_Fetch_Metadata_es_rechazado(string sitio)
    {
        using var cliente = Fabrica.CrearCliente();
        cliente.Http.DefaultRequestHeaders.Add("Sec-Fetch-Site", sitio);

        var respuesta = await cliente.EntrarAsync("alguien", "una-contrasena-cualquiera");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await respuesta.CodigoAsync()).ShouldBe("Prohibido");
    }

    [Fact]
    public async Task Sin_Fetch_Metadata_un_Origin_ajeno_es_rechazado()
    {
        using var cliente = Fabrica.CrearCliente();
        cliente.Http.DefaultRequestHeaders.Add("Origin", "https://sitio-malicioso.example");

        var respuesta = await cliente.EntrarAsync("alguien", "una-contrasena-cualquiera");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sin_Fetch_Metadata_el_Origin_propio_pasa_el_filtro()
    {
        using var cliente = Fabrica.CrearCliente();
        cliente.Http.DefaultRequestHeaders.Add("Origin", FabricaTiie.UrlPublica);

        var respuesta = await cliente.EntrarAsync("alguien", "una-contrasena-cualquiera");

        // Llega al caso de uso: el usuario no existe.
        (await respuesta.CodigoAsync()).ShouldBe("CredencialesInvalidas");
    }

    [Fact]
    public async Task Una_ruta_api_inexistente_responde_404_en_json_y_nunca_index_html()
    {
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/api/no-existe");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await respuesta.CodigoAsync()).ShouldBe("NoEncontrado");
    }

    [Fact]
    public async Task Un_json_mal_formado_responde_400_SolicitudInvalida_en_espanol()
    {
        using var cliente = Fabrica.CrearCliente();
        using var cuerpo = new StringContent("{\"usuario\":", Encoding.UTF8, "application/json");

        var respuesta = await cliente.Http.PostAsync(new Uri("/api/admin/auth/entrar", UriKind.Relative), cuerpo);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problema = await respuesta.ProblemaAsync();
        problema.GetProperty("codigo").GetString().ShouldBe("SolicitudInvalida");
        problema.GetProperty("detail").GetString().ShouldBe("El cuerpo de la solicitud no es un JSON válido.");
        problema.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task La_configuracion_publica_trae_lo_que_la_spa_necesita()
    {
        using var cliente = Fabrica.CrearCliente();

        var configuracion = await (await cliente.GetAsync("/api/publico/configuracion"))
            .Content.ReadFromJsonAsync<Dictionary<string, string>>();

        configuracion!["claveSitioCaptcha"].ShouldBe("clave-sitio-pruebas");
        configuracion["versionAvisoPrivacidad"].ShouldBe("2026-01");
        configuracion["zonaHoraria"].ShouldBe("America/Mazatlan");
        configuracion.ShouldNotContainKey("claveSecreta");
    }

    [Fact]
    public async Task Salud_responde_200_cuando_la_base_esta_disponible()
    {
        using var cliente = Fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/salud");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
