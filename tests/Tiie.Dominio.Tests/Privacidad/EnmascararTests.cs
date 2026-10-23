using Tiie.Dominio.Privacidad;

namespace Tiie.Dominio.Tests.Privacidad;

public sealed class EnmascararTests
{
    [Fact]
    public void La_curp_conserva_cuatro_caracteres_al_inicio_y_dos_al_final()
    {
        Enmascarar.Curp("GOMA080515MSLRRNA5").ShouldBe("GOMA************A5");
    }

    [Fact]
    public void El_correo_conserva_la_primera_letra_y_el_dominio()
    {
        Enmascarar.Correo("ana@example.com").ShouldBe("a***@example.com");
    }

    [Fact]
    public void El_celular_conserva_los_ultimos_cuatro_digitos()
    {
        Enmascarar.Celular("6671234567").ShouldBe("******4567");
    }

    [Fact]
    public void El_nombre_conserva_la_inicial_de_cada_palabra()
    {
        Enmascarar.Nombre("Ana  Gómez Martínez").ShouldBe("A*** G*** M***");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Un_valor_vacio_queda_vacio(string? valor)
    {
        Enmascarar.Curp(valor).ShouldBeEmpty();
        Enmascarar.Correo(valor).ShouldBeEmpty();
        Enmascarar.Celular(valor).ShouldBeEmpty();
        Enmascarar.Nombre(valor).ShouldBeEmpty();
    }

    [Fact]
    public void Un_valor_corto_se_oculta_completo()
    {
        Enmascarar.Curp("ABC").ShouldBe("***");
        Enmascarar.Celular("123").ShouldBe("***");
        Enmascarar.Correo("sin-arroba").ShouldBe("***");
    }
}
