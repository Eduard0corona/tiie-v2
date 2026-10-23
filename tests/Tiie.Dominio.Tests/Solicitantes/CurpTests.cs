using System.Text.Json;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Dominio.Tests.Solicitantes;

public sealed class CurpTests
{
    public sealed record VectorCurp(string Curp, bool Valida, string? FechaNacimiento, string? Sexo, string Nota)
    {
        public override string ToString() => Nota;
    }

    public static TheoryData<VectorCurp> Vectores()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectores-curp.json"));
        var vectores = JsonSerializer.Deserialize<List<VectorCurp>>(json, JsonSerializerOptions.Web)!;
        return [.. vectores];
    }

    [Theory]
    [MemberData(nameof(Vectores))]
    public void RN15_los_vectores_compartidos_dan_el_resultado_esperado(VectorCurp vector)
    {
        var valida = Curp.TryInterpretar(vector.Curp, out var datos);

        valida.ShouldBe(vector.Valida);
        if (vector.Valida)
        {
            datos!.FechaNacimiento.ShouldBe(DateOnly.Parse(vector.FechaNacimiento!, System.Globalization.CultureInfo.InvariantCulture));
            datos.Sexo.ShouldBe(vector.Sexo![0]);
            datos.Curp.ShouldBe(Curp.Normalizar(vector.Curp));
        }
    }

    [Fact]
    public void RN15_el_digito_verificador_del_ejemplo_de_la_documentacion_es_5()
    {
        Curp.CalcularDigitoVerificador("GOMA080515MSLRRNA").ShouldBe(5);
    }

    [Fact]
    public void RN15_normaliza_a_mayusculas_y_sin_espacios()
    {
        Curp.Normalizar(" goma 080515 mslrrna5\t").ShouldBe("GOMA080515MSLRRNA5");
    }

    [Fact]
    public void RN15_fecha_de_nacimiento_y_sexo_se_derivan_de_la_curp()
    {
        var datos = Curp.Interpretar("GOMA080515MSLRRNA5");

        datos.FechaNacimiento.ShouldBe(new DateOnly(2008, 5, 15));
        datos.Sexo.ShouldBe('M');
    }

    [Fact]
    public void RN15_interpretar_una_curp_invalida_es_error_de_validacion()
    {
        var error = Should.Throw<ReglaNegocioException>(() => Curp.Interpretar("GOMA080515MSLRRNA6"));

        error.Codigo.ShouldBe(CodigoError.Validacion);
    }

    [Fact]
    public void RN15_una_curp_nula_no_es_valida()
    {
        Curp.EsValida(null).ShouldBeFalse();
    }
}
