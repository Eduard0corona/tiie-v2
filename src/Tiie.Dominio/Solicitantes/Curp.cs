using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Tiie.Dominio.Errores;

namespace Tiie.Dominio.Solicitantes;

/// <summary>Datos que se derivan de una CURP válida (RN-15).</summary>
public sealed record DatosCurp(string Curp, DateOnly FechaNacimiento, char Sexo);

/// <summary>
/// Validación de la CURP (RN-15). La misma regla existe en <c>src/lib/curp.ts</c>; ambas se prueban
/// con <c>tests/vectores-curp.json</c>.
/// </summary>
public static partial class Curp
{
    public const int Longitud = 18;

    private const string Diccionario = "0123456789ABCDEFGHIJKLMNÑOPQRSTUVWXYZ";

    /// <summary>Mayúsculas y sin espacios.</summary>
    public static string Normalizar(string curp)
    {
        ArgumentNullException.ThrowIfNull(curp);
        return string.Concat(curp.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }

    public static bool EsValida(string? curp) => TryInterpretar(curp, out _);

    /// <summary>Normaliza, valida y deriva fecha de nacimiento y sexo.</summary>
    /// <exception cref="ReglaNegocioException">Con <see cref="CodigoError.Validacion"/> si no es válida.</exception>
    public static DatosCurp Interpretar(string curp) =>
        TryInterpretar(curp, out var datos)
            ? datos
            : throw new ReglaNegocioException(CodigoError.Validacion, "La CURP no es válida.");

    public static bool TryInterpretar(string? curp, [NotNullWhen(true)] out DatosCurp? datos)
    {
        datos = null;
        if (curp is null)
            return false;

        var normalizada = Normalizar(curp);
        if (!Formato().IsMatch(normalizada))
            return false;

        // El siglo lo da la posición 17: dígito = 1900 a 1999, letra = 2000 en adelante.
        var siglo = char.IsAsciiDigit(normalizada[16]) ? 1900 : 2000;
        var anio = siglo + int.Parse(normalizada.AsSpan(4, 2), CultureInfo.InvariantCulture);
        var mes = int.Parse(normalizada.AsSpan(6, 2), CultureInfo.InvariantCulture);
        var dia = int.Parse(normalizada.AsSpan(8, 2), CultureInfo.InvariantCulture);
        if (dia > DateTime.DaysInMonth(anio, mes))
            return false;

        if (normalizada[17] - '0' != CalcularDigitoVerificador(normalizada.AsSpan(0, 17)))
            return false;

        datos = new DatosCurp(normalizada, new DateOnly(anio, mes, dia), normalizada[10]);
        return true;
    }

    /// <summary>Dígito verificador de la posición 18 a partir de las primeras 17 posiciones.</summary>
    public static int CalcularDigitoVerificador(ReadOnlySpan<char> primeras17)
    {
        if (primeras17.Length != Longitud - 1)
            throw new ArgumentException("Se esperan 17 caracteres.", nameof(primeras17));

        var suma = 0;
        for (var i = 0; i < primeras17.Length; i++)
            suma += Diccionario.IndexOf(primeras17[i], StringComparison.Ordinal) * (18 - i);
        return (10 - (suma % 10)) % 10;
    }

    [GeneratedRegex(
        "^[A-Z][AEIOUX][A-Z]{2}[0-9]{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[HMX]" +
        "(AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)" +
        "[B-DF-HJ-NP-TV-Z]{3}[A-Z0-9][0-9]$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
