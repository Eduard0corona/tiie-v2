using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Tiie.Web.Seguridad;

/// <summary>Reglas propias de la cookie <c>tiie_sesion</c>, además del sello de seguridad de Identity.</summary>
public static class ValidadorSesion
{
    /// <summary>Marca del inicio real de la sesión; la renovación por inactividad la conserva.</summary>
    public const string Inicio = ".tiie.inicio";

    public static void MarcarInicio(CookieSigningInContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var ahora = contexto.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        contexto.Properties.Items.TryAdd(Inicio, ahora.ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>Rechaza la sesión si pasó <paramref name="maxima"/> desde que inició, aunque siga activa.</summary>
    public static void ExigirDuracionMaxima(CookieValidatePrincipalContext contexto, TimeSpan maxima)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var ahora = contexto.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var valida = contexto.Properties.Items.TryGetValue(Inicio, out var texto)
            && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var inicio)
            && ahora - inicio <= maxima;
        if (!valida)
            contexto.RejectPrincipal();
    }

    /// <summary>
    /// Cookie del ciudadano: rechaza la sesión si el token de la cita ya venció (RN-16).
    /// </summary>
    public static Task ValidarCitaAsync(CookieValidatePrincipalContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var ahora = contexto.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var vence = contexto.Principal?.FindFirst(Aplicacion.Seguridad.ClaimsTiie.TokenVenceEn)?.Value;
        if (!DateTimeOffset.TryParse(vence, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var venceEn) || venceEn < ahora)
            contexto.RejectPrincipal();
        return Task.CompletedTask;
    }
}
