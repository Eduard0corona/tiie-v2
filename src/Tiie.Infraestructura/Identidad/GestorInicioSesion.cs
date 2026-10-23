using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tiie.Infraestructura.Identidad;

/// <summary>
/// <see cref="SignInManager{TUser}"/> que además exige <see cref="Usuario.Activo"/>: un usuario inactivo
/// no inicia sesión y sus sesiones dejan de valer en la siguiente validación del sello de seguridad.
/// </summary>
public sealed class GestorInicioSesion(
    UserManager<Usuario> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<Usuario> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<Usuario>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<Usuario> confirmation)
    : SignInManager<Usuario>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(Usuario user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Activo && await base.CanSignInAsync(user);
    }

    public override async Task<Usuario?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        var usuario = await base.ValidateSecurityStampAsync(principal);
        return usuario is { Activo: true } ? usuario : null;
    }
}
