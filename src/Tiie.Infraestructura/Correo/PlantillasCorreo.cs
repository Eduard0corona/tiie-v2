using System.Collections.Concurrent;
using System.Net;
using Tiie.Aplicacion.Puertos;

namespace Tiie.Infraestructura.Correo;

/// <summary>
/// Plantillas incrustadas en <c>Correo/Plantillas</c>: un <c>.html</c> y un <c>.txt</c> por tipo, más el
/// diseño común. Los marcadores tienen la forma <c>{{Nombre}}</c>; en HTML cada valor se codifica.
/// </summary>
public sealed class PlantillasCorreo : IPlantillasCorreo
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public MensajeCorreo Invitacion(string destinatario, string nombre, string enlace) =>
        Armar(destinatario, "Activa tu cuenta del panel de citas TIIE", "invitacion",
            new Dictionary<string, string> { ["Nombre"] = nombre, ["Enlace"] = enlace });

    public MensajeCorreo Restablecimiento(string destinatario, string nombre, string enlace) =>
        Armar(destinatario, "Restablece tu contraseña del panel de citas TIIE", "restablecimiento",
            new Dictionary<string, string> { ["Nombre"] = nombre, ["Enlace"] = enlace });

    private static MensajeCorreo Armar(string destinatario, string asunto, string plantilla, Dictionary<string, string> valores)
    {
        var html = Reemplazar(Leer("_diseno.html"), new Dictionary<string, string>
        {
            ["Asunto"] = WebUtility.HtmlEncode(asunto),
            ["Contenido"] = Reemplazar(Leer($"{plantilla}.html"), valores, codificar: true),
        }, codificar: false);
        var texto = Reemplazar(Leer($"{plantilla}.txt"), valores, codificar: false);
        return new MensajeCorreo(destinatario, asunto, html, texto);
    }

    private static string Reemplazar(string plantilla, IReadOnlyDictionary<string, string> valores, bool codificar)
    {
        foreach (var (marcador, valor) in valores)
            plantilla = plantilla.Replace($"{{{{{marcador}}}}}", codificar ? WebUtility.HtmlEncode(valor) : valor, StringComparison.Ordinal);
        return plantilla;
    }

    private static string Leer(string archivo) => Cache.GetOrAdd(archivo, static a =>
    {
        var recurso = $"Tiie.Infraestructura.Correo.Plantillas.{a}";
        using var flujo = typeof(PlantillasCorreo).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"No existe la plantilla de correo {a}.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    });
}
