using Tiie.Dominio.Sitio;

namespace Tiie.Dominio.Tests.Sitio;

public sealed class ReglasPortadaTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 2);

    [Fact]
    public void El_aviso_se_muestra_dentro_de_su_rango_inclusivo()
    {
        ReglasPortada.Estado("Cerrado el 2 de noviembre", Hoy, Hoy, Hoy).ShouldBe(EstadoAviso.Visible);
        ReglasPortada.Estado("Aviso", Hoy.AddDays(1), null, Hoy).ShouldBe(EstadoAviso.Programado);
        ReglasPortada.Estado("Aviso", null, Hoy.AddDays(-1), Hoy).ShouldBe(EstadoAviso.Vencido);
        ReglasPortada.Estado("Aviso", null, null, Hoy).ShouldBe(EstadoAviso.Visible);
    }

    [Fact]
    public void Sin_texto_no_hay_aviso_aunque_tenga_fechas()
    {
        ReglasPortada.Estado("  ", Hoy, Hoy, Hoy).ShouldBe(EstadoAviso.SinAviso);
        ReglasPortada.Estado(null, null, null, Hoy).ShouldBe(EstadoAviso.SinAviso);
    }

    [Theory]
    [InlineData("/recuperar")]
    [InlineData("/#preguntas")]
    [InlineData("https://www.gob.mx/curp/")]
    [InlineData("mailto:tiie@sinaloa.gob.mx")]
    [InlineData("tel:6671234567")]
    public void Se_permiten_rutas_del_sitio_https_correo_y_telefono(string destino) =>
        ReglasPortada.DestinoPermitido(destino).ShouldBeTrue();

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://ejemplo.com")]
    [InlineData("//ejemplo.com")]
    [InlineData("/\\ejemplo.com")]
    [InlineData("data:text/html,hola")]
    [InlineData("recuperar")]
    public void Se_rechazan_enlaces_que_pueden_llevar_fuera_del_sitio_o_ejecutar_codigo(string destino) =>
        ReglasPortada.DestinoPermitido(destino).ShouldBeFalse();

    [Fact]
    public void Los_enlaces_se_leen_con_la_sintaxis_texto_entre_corchetes_y_destino_entre_parentesis()
    {
        var texto = "Revisa el [aviso de privacidad](/aviso-de-privacidad) o [gob.mx/curp](https://www.gob.mx/curp/). [sin destino] (/x)";

        ReglasPortada.Destinos(texto).ShouldBe(["/aviso-de-privacidad", "https://www.gob.mx/curp/"]);
        ReglasPortada.EnlacesPermitidos(texto).ShouldBeTrue();
        ReglasPortada.EnlacesPermitidos("Haz [clic](javascript:void0)").ShouldBeFalse();
        // Con paréntesis en el destino no hay enlace: la SPA lo muestra como texto.
        ReglasPortada.Destinos("Haz [clic](javascript:alert(1))").ShouldBeEmpty();
        ReglasPortada.EnlacesPermitidos(null).ShouldBeTrue();
    }
}
