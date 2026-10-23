using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Tiie.Aplicacion.MiCita;
using Tiie.Aplicacion.Seguridad;

namespace Tiie.Web.Seguridad;

/// <summary>
/// Sesión del ciudadano con la cookie <c>tiie_cita</c>, limitada a <c>/api/mi-cita</c> (ADR 0005). Lleva la cita
/// y el vencimiento del token; <see cref="ValidadorSesion.ValidarCitaAsync"/> la rechaza cuando el token vence.
/// </summary>
public static class AccesoCita
{
    public static ClaimsPrincipal CrearPrincipal(int citaId, DateTimeOffset tokenVenceEn) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimsTiie.CitaId, citaId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimsTiie.TokenVenceEn, tokenVenceEn.ToString("O", CultureInfo.InvariantCulture)),
            ],
            EsquemasTiie.Cita));

    public static Task IniciarSesionAsync(HttpContext http, AccesoMiCita acceso)
    {
        ArgumentNullException.ThrowIfNull(acceso);
        return http.SignInAsync(EsquemasTiie.Cita, CrearPrincipal(acceso.CitaId, acceso.TokenVenceEn));
    }

    public static Task CerrarSesionAsync(HttpContext http) => http.SignOutAsync(EsquemasTiie.Cita);

    /// <summary>Cita de la sesión; la política <c>Cita</c> ya exigió el claim.</summary>
    public static int CitaId(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return int.Parse(principal.FindFirstValue(ClaimsTiie.CitaId)!, CultureInfo.InvariantCulture);
    }
}
