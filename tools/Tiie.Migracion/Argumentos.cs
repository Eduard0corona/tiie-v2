using System.Globalization;

namespace Tiie.Migracion;

/// <summary>Opciones <c>--nombre valor</c> y banderas <c>--nombre</c> de la línea de comandos.</summary>
public sealed class Argumentos
{
    private static readonly HashSet<string> ConValor =
        ["origen", "destino", "fecha-corte", "mapa-roles", "decisiones", "salida"];

    private static readonly HashSet<string> Banderas = ["reemplazar-plantillas", "llaves-locales"];

    private readonly Dictionary<string, string> _valores = [];
    private readonly HashSet<string> _banderas = [];

    public static Argumentos Parsear(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var resultado = new Argumentos();
        using var e = args.GetEnumerator();
        while (e.MoveNext())
        {
            var nombre = e.Current.StartsWith("--", StringComparison.Ordinal) ? e.Current[2..] : throw new FormatException($"Argumento inesperado: {e.Current}");
            if (Banderas.Contains(nombre))
                resultado._banderas.Add(nombre);
            else if (ConValor.Contains(nombre))
                resultado._valores[nombre] = e.MoveNext() ? e.Current : throw new FormatException($"Falta el valor de --{nombre}.");
            else
                throw new FormatException($"Opción desconocida: --{nombre}.");
        }
        return resultado;
    }

    public string? Opcional(string nombre) => _valores.GetValueOrDefault(nombre);

    public string Exigir(string nombre) => Opcional(nombre) ?? throw new FormatException($"Falta --{nombre}.");

    public bool Bandera(string nombre) => _banderas.Contains(nombre);

    public DateOnly? Fecha(string nombre) =>
        Opcional(nombre) is not { } texto
            ? null
            : DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                ? fecha
                : throw new FormatException($"--{nombre} debe tener la forma aaaa-mm-dd.");
}
