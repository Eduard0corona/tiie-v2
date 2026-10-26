using System.Text.RegularExpressions;

namespace Tiie.Dominio.Sitio;

/// <summary>Si el aviso de la portada se muestra hoy.</summary>
public enum EstadoAviso
{
    SinAviso,
    Programado,
    Visible,
    Vencido,
}

/// <summary>
/// Reglas del contenido de la portada: vigencia del aviso y enlaces permitidos. El texto se guarda plano; los
/// enlaces se escriben <c>[texto](destino)</c> y la SPA los muestra con la misma expresión (<c>src/lib/enlaces.ts</c>).
/// </summary>
public static partial class ReglasPortada
{
    /// <summary>El aviso se muestra con texto y con hoy dentro del rango inclusivo; una fecha vacía no limita.</summary>
    public static EstadoAviso Estado(string? texto, DateOnly? desde, DateOnly? hasta, DateOnly hoy)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return EstadoAviso.SinAviso;
        if (desde is { } d && hoy < d)
            return EstadoAviso.Programado;
        if (hasta is { } h && hoy > h)
            return EstadoAviso.Vencido;
        return EstadoAviso.Visible;
    }

    /// <summary>Destinos de los enlaces <c>[texto](destino)</c> del texto, en orden.</summary>
    public static IEnumerable<string> Destinos(string? texto) =>
        string.IsNullOrEmpty(texto) ? [] : Enlace().Matches(texto).Select(m => m.Groups[2].Value);

    /// <summary>
    /// Un enlace lleva a una ruta del sitio (<c>/agendar</c>, <c>/#preguntas</c>), a una página segura
    /// (<c>https://</c>), a un correo (<c>mailto:</c>) o a un teléfono (<c>tel:</c>). Nada de <c>javascript:</c>,
    /// <c>http://</c> ni rutas de otro dominio como <c>//ejemplo.com</c>.
    /// </summary>
    public static bool DestinoPermitido(string destino)
    {
        ArgumentNullException.ThrowIfNull(destino);
        return (destino.StartsWith('/') && !destino.StartsWith("//", StringComparison.Ordinal) && !destino.Contains('\\', StringComparison.Ordinal))
            || destino.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || destino.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || destino.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool EnlacesPermitidos(string? texto) => Destinos(texto).All(DestinoPermitido);

    [GeneratedRegex(@"\[([^\[\]\n]+)\]\(([^()\s]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex Enlace();
}
