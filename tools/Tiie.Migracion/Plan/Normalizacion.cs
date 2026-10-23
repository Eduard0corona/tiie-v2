using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tiie.Migracion.Plan;

public static partial class Normalizacion
{
    /// <summary>Clave para agrupar trámites por nombre: sin acentos, en mayúsculas y con espacios simples.</summary>
    public static string ClaveNombre(string nombre)
    {
        ArgumentNullException.ThrowIfNull(nombre);
        var sinAcentos = new StringBuilder(nombre.Length);
        foreach (var c in nombre.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sinAcentos.Append(c);
        return Espacios().Replace(sinAcentos.ToString().Normalize(NormalizationForm.FormC), " ").Trim().ToUpperInvariant();
    }

    /// <summary>Texto con espacios simples, o vacío si no tiene contenido.</summary>
    public static string? Limpiar(string? texto, int? maximo = null)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;
        var limpio = Espacios().Replace(texto, " ").Trim();
        return maximo is { } m && limpio.Length > m ? limpio[..m].TrimEnd() : limpio;
    }

    /// <summary>Solo dígitos; vacío si no quedan exactamente <paramref name="digitos"/>.</summary>
    public static string? Digitos(string? texto, int digitos)
    {
        if (texto is null)
            return null;
        var solo = string.Concat(texto.Where(char.IsAsciiDigit));
        return solo.Length == digitos ? solo : null;
    }

    /// <summary>
    /// El valor más frecuente, sin contar vacíos. Un empate lo gana el que aparece primero, así el resultado
    /// no depende del orden en que la base devuelve las filas (el llamador ordena por ID).
    /// </summary>
    public static T? MasComun<T>(IEnumerable<T?> valores)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(valores);
        var conteos = new Dictionary<T, (int Veces, int Primero)>();
        var i = 0;
        foreach (var valor in valores)
        {
            if (valor is not null && !(valor is string s && string.IsNullOrWhiteSpace(s)))
                conteos[valor] = conteos.TryGetValue(valor, out var c) ? (c.Veces + 1, c.Primero) : (1, i);
            i++;
        }
        return conteos.Count == 0 ? null : conteos.OrderByDescending(c => c.Value.Veces).ThenBy(c => c.Value.Primero).First().Key;
    }

    /// <summary><see cref="MasComun{T}(IEnumerable{T})"/> para valores como el costo.</summary>
    public static T? MasComunValor<T>(IEnumerable<T?> valores)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(valores);
        return MasComun(valores.Select(v => v is { } x ? new Caja<T>(x) : null))?.Valor;
    }

    private sealed record Caja<T>(T Valor);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
