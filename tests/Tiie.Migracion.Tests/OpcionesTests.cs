using Tiie.Migracion.Plan;
using Tiie.Migracion.Reportes;

namespace Tiie.Migracion.Tests;

public sealed class OpcionesTests
{
    [Fact]
    public void El_mapa_de_roles_acepta_los_tres_roles_sin_importar_mayusculas()
    {
        var mapa = MapaRoles.Parsear(" 0=capturista, 1=Supervisor ,2=ADMINISTRADOR");

        (mapa.Rol(0), mapa.Rol(1), mapa.Rol(2), mapa.Rol(3)).ShouldBe(("Capturista", "Supervisor", "Administrador", (string?)null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("x=Capturista")]
    [InlineData("0=Jefe")]
    [InlineData("0=Capturista,0=Supervisor")]
    public void Un_mapa_de_roles_invalido_se_rechaza(string texto) =>
        Should.Throw<FormatException>(() => MapaRoles.Parsear(texto));

    [Fact]
    public void Las_decisiones_normalizan_la_curp_e_ignoran_el_encabezado()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(ruta, ["Curp,FolioConservar", "goma080515mslrrna5, F-2", ""]);

            Decisiones.Leer(ruta).ShouldBe(new Dictionary<string, string> { ["GOMA080515MSLRRNA5"] = "F-2" });
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void Una_curp_con_dos_decisiones_se_rechaza()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(ruta, ["GOMA080515MSLRRNA5,F-1", "GOMA080515MSLRRNA5,F-2"]);

            Should.Throw<FormatException>(() => Decisiones.Leer(ruta));
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void Los_argumentos_distinguen_opciones_con_valor_y_banderas()
    {
        var a = Argumentos.Parsear(["--origen", "Server=x", "--llaves-locales", "--fecha-corte", "2026-11-30"]);

        a.Exigir("origen").ShouldBe("Server=x");
        a.Bandera("llaves-locales").ShouldBeTrue();
        a.Bandera("reemplazar-plantillas").ShouldBeFalse();
        a.Fecha("fecha-corte").ShouldBe(new DateOnly(2026, 11, 30));
        Should.Throw<FormatException>(() => a.Exigir("destino"));
    }

    [Theory]
    [InlineData("--desconocida", "x")]
    [InlineData("--origen")]
    [InlineData("suelto")]
    public void Los_argumentos_invalidos_se_rechazan(params string[] args) =>
        Should.Throw<FormatException>(() => Argumentos.Parsear(args));

    [Fact]
    public async Task Sin_comando_muestra_la_ayuda_y_un_comando_desconocido_termina_con_codigo_2()
    {
        using var salida = new StringWriter();
        using var errores = new StringWriter();

        (await Consola.EjecutarAsync([], salida, errores, CancellationToken.None)).ShouldBe(2);
        salida.ToString().ShouldContain("analizar");
        (await Consola.EjecutarAsync(["borrar"], salida, errores, CancellationToken.None)).ShouldBe(2);
        errores.ToString().ShouldContain("Comando desconocido");
    }

    [Theory]
    [InlineData("Expedición  de   TIIE", "EXPEDICION DE TIIE")]
    [InlineData(" reposición ", "REPOSICION")]
    [InlineData("Ñandú", "NANDU")]
    public void La_clave_de_nombre_quita_acentos_y_espacios_repetidos(string nombre, string clave) =>
        Normalizacion.ClaveNombre(nombre).ShouldBe(clave);

    [Fact]
    public void El_valor_mas_comun_ignora_vacios_y_desempata_por_el_primero()
    {
        Normalizacion.MasComun(["b", null, "a", " ", "a", "b"]).ShouldBe("b");
        Normalizacion.MasComun<string>([null, " "]).ShouldBeNull();
        Normalizacion.MasComunValor<decimal>([150m, null, 200m, 200m]).ShouldBe(200m);
    }

    [Fact]
    public void Los_digitos_solo_valen_con_la_longitud_exacta()
    {
        Normalizacion.Digitos("(667) 123-4567", 10).ShouldBe("6671234567");
        Normalizacion.Digitos("123", 10).ShouldBeNull();
    }

    [Fact]
    public async Task El_reporte_escapa_comas_y_comillas()
    {
        var carpeta = Directory.CreateTempSubdirectory();
        try
        {
            var reporte = new Reporte("prueba.csv", "A", "B");
            reporte.Agregar("uno, dos", "dice \"hola\"");
            await reporte.GuardarAsync(carpeta.FullName, CancellationToken.None);

            (await File.ReadAllLinesAsync(Path.Combine(carpeta.FullName, "prueba.csv"))).ShouldBe(["A,B", "\"uno, dos\",\"dice \"\"hola\"\"\""]);
        }
        finally
        {
            carpeta.Delete(recursive: true);
        }
    }
}
