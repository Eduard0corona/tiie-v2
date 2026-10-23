using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;

namespace Tiie.Aplicacion.Tests.Opciones;

public sealed class OpcionesTests
{
    private static ServiceProvider Proveedor(Dictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        return new ServiceCollection().AgregarAplicacion(configuracion).BuildServiceProvider();
    }

    private static Dictionary<string, string?> Completa() => new()
    {
        ["Sitio:UrlPublica"] = "https://tiie.test",
        ["Sitio:VersionAvisoPrivacidad"] = "2026-01",
        ["Captcha:ClaveSitio"] = "sitio",
        ["Captcha:ClaveSecreta"] = "secreta",
    };

    [Fact]
    public void Los_valores_por_omision_de_la_agenda_son_los_documentados()
    {
        using var proveedor = Proveedor(Completa());
        var agenda = proveedor.GetRequiredService<IOptions<OpcionesAgenda>>().Value;

        agenda.ZonaHoraria.ShouldBe("America/Mazatlan");
        agenda.DiasVentanaPredeterminados.ShouldBe((short)49);
        agenda.UmbralPocaDisponibilidad.ShouldBe(0.45);
        agenda.DiasAnticipacionCambios.ShouldBe(1);
        agenda.HoraRecordatorio.ShouldBe(new TimeOnly(9, 0));
        agenda.MesesRetencion.ShouldBeNull();
    }

    [Fact]
    public void Una_zona_horaria_inexistente_no_pasa_la_validacion()
    {
        var valores = Completa();
        valores["Agenda:ZonaHoraria"] = "Marte/Olympus";
        using var proveedor = Proveedor(valores);

        Should.Throw<OptionsValidationException>(() => proveedor.GetRequiredService<IOptions<OpcionesAgenda>>().Value);
    }

    [Fact]
    public void Sin_url_publica_el_sitio_no_pasa_la_validacion()
    {
        var valores = Completa();
        valores.Remove("Sitio:UrlPublica");
        using var proveedor = Proveedor(valores);

        Should.Throw<OptionsValidationException>(() => proveedor.GetRequiredService<IOptions<OpcionesSitio>>().Value);
    }

    [Fact]
    public void La_url_publica_se_usa_sin_diagonal_final()
    {
        new OpcionesSitio { UrlPublica = "https://tiie.test/" }.UrlBase.ShouldBe("https://tiie.test");
    }
}
