using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Notificaciones;

namespace Tiie.Infraestructura.Correo;

/// <summary>
/// Plantillas incrustadas en <c>Correo/Plantillas</c>: un <c>.html</c> y un <c>.txt</c> por tipo, más el
/// diseño común. Los marcadores tienen la forma <c>{{Nombre}}</c>; en HTML cada valor se codifica.
/// </summary>
public sealed class PlantillasCorreo : IPlantillasCorreo
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public MensajeCorreo Invitacion(string destinatario, string nombre, string enlace) =>
        Armar(destinatario, "Activa tu cuenta del panel de citas TIIE", "invitacion", "_diseno.html",
            new Dictionary<string, string> { ["Nombre"] = nombre, ["Enlace"] = enlace });

    public MensajeCorreo Restablecimiento(string destinatario, string nombre, string enlace) =>
        Armar(destinatario, "Restablece tu contraseña del panel de citas TIIE", "restablecimiento", "_diseno.html",
            new Dictionary<string, string> { ["Nombre"] = nombre, ["Enlace"] = enlace });

    public MensajeCorreo Notificacion(TipoNotificacion tipo, string destinatario, DatosNotificacion datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var (plantilla, asunto) = tipo switch
        {
            TipoNotificacion.Confirmacion => ("confirmacion", $"Tu cita para la TIIE: {datos.Fecha} a las {datos.Hora}"),
            TipoNotificacion.Recordatorio => ("recordatorio", "Mañana es tu cita para la TIIE"),
            TipoNotificacion.Cancelacion => ("cancelacion", $"Tu cita {datos.Folio} fue cancelada"),
            TipoNotificacion.Reprogramacion => ("reprogramacion", $"Tu cita cambió: {datos.Fecha} a las {datos.Hora}"),
            TipoNotificacion.EnlaceRecuperacion => ("enlace-recuperacion", "Enlace para consultar tu cita"),
            TipoNotificacion.CierrePorBloqueo => ("cierre-bloqueo", "Tu cita fue cancelada por cierre del módulo"),
            TipoNotificacion.AvisoMigracion => ("aviso-migracion", "Tu cita sigue vigente: este es tu nuevo enlace"),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de notificación sin plantilla."),
        };

        var valores = new Dictionary<string, string>
        {
            ["Nombre"] = datos.Nombre,
            ["Folio"] = datos.Folio,
            ["Fecha"] = datos.Fecha,
            ["Hora"] = datos.Hora,
            ["Modulo"] = datos.Modulo,
            ["Direccion"] = datos.Direccion,
            ["Tramite"] = datos.Tramite,
            ["Enlace"] = datos.Enlace,
            ["AvisoPrivacidad"] = datos.AvisoPrivacidad,
        };

        // Requisitos y motivo llevan marcado propio: se arman aquí, con cada valor ya codificado.
        var html = Reemplazar(Leer($"{plantilla}.html"), valores, codificar: true)
            .Replace("{{Requisitos}}", ListaHtml(datos.Requisitos), StringComparison.Ordinal)
            .Replace("{{Motivo}}", datos.Motivo is null ? "" : $"<p><strong>Motivo:</strong> {WebUtility.HtmlEncode(datos.Motivo)}</p>", StringComparison.Ordinal);
        var texto = Reemplazar(Leer($"{plantilla}.txt"), valores, codificar: false)
            .Replace("{{Requisitos}}", string.Join('\n', datos.Requisitos.Select(r => $"- {r}")), StringComparison.Ordinal)
            .Replace("{{Motivo}}", datos.Motivo is null ? "" : $"Motivo: {datos.Motivo}\n", StringComparison.Ordinal);

        var completo = Reemplazar(Leer("_diseno-ciudadano.html"), new Dictionary<string, string>
        {
            ["Asunto"] = WebUtility.HtmlEncode(asunto),
            ["AvisoPrivacidad"] = WebUtility.HtmlEncode(datos.AvisoPrivacidad),
            ["Contenido"] = html,
        }, codificar: false);
        return new MensajeCorreo(destinatario, asunto, completo, texto);
    }

    private static MensajeCorreo Armar(
        string destinatario, string asunto, string plantilla, string diseno, Dictionary<string, string> valores)
    {
        var html = Reemplazar(Leer(diseno), new Dictionary<string, string>
        {
            ["Asunto"] = WebUtility.HtmlEncode(asunto),
            ["Contenido"] = Reemplazar(Leer($"{plantilla}.html"), valores, codificar: true),
        }, codificar: false);
        var texto = Reemplazar(Leer($"{plantilla}.txt"), valores, codificar: false);
        return new MensajeCorreo(destinatario, asunto, html, texto);
    }

    private static string ListaHtml(IReadOnlyList<string> elementos)
    {
        if (elementos.Count == 0)
            return "";
        var sb = new StringBuilder("<ul style=\"padding-left:20px;margin:0 0 16px;\">");
        foreach (var elemento in elementos)
            sb.Append("<li style=\"margin:4px 0;\">").Append(WebUtility.HtmlEncode(elemento)).Append("</li>");
        return sb.Append("</ul>").ToString();
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
