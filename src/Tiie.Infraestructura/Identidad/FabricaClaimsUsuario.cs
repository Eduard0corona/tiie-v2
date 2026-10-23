using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Seguridad;
using Tiie.Infraestructura.Datos;

namespace Tiie.Infraestructura.Identidad;

/// <summary>
/// Agrega a la sesión <c>mfa</c>, <c>nombre_completo</c> y un claim <c>modulo</c> por módulo asignado.
/// Se vuelve a ejecutar cada vez que se valida el sello de seguridad (cada 5 minutos).
/// </summary>
public sealed class FabricaClaimsUsuario(
    UserManager<Usuario> userManager,
    RoleManager<Rol> roleManager,
    IOptions<IdentityOptions> options,
    TiieDbContext db)
    : UserClaimsPrincipalFactory<Usuario, Rol>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var identidad = await base.GenerateClaimsAsync(user);

        identidad.AddClaim(new Claim(ClaimsTiie.Mfa, user.TwoFactorEnabled ? "true" : "false"));
        identidad.AddClaim(new Claim(ClaimsTiie.NombreCompleto, user.NombreCompleto));

        var modulos = await db.UsuariosModulos.AsNoTracking()
            .Where(um => um.UsuarioId == user.Id)
            .Select(um => um.ModuloId)
            .ToListAsync();
        foreach (var moduloId in modulos)
            identidad.AddClaim(new Claim(ClaimsTiie.Modulo, moduloId.ToString(CultureInfo.InvariantCulture)));

        return identidad;
    }
}
