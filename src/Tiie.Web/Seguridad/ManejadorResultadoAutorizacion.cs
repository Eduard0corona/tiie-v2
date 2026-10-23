using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Web.Errores;

namespace Tiie.Web.Seguridad;

/// <summary>
/// Una SPA no sigue redirecciones de login: sin sesión responde 401 <c>NoAutenticado</c>; sin permiso,
/// 403 <c>Prohibido</c>, o <c>MfaRequerido</c> si la sesión aún no configura MFA.
/// </summary>
public sealed class ManejadorResultadoAutorizacion : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _predeterminado = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Challenged)
        {
            await ErroresApi.Problema(CodigoError.NoAutenticado).ExecuteAsync(context);
            return;
        }
        if (authorizeResult.Forbidden)
        {
            var sinMfa = context.User.Identity?.IsAuthenticated == true
                && context.User.FindFirst(ClaimsTiie.Mfa)?.Value != "true";
            await ErroresApi.Problema(sinMfa ? CodigoError.MfaRequerido : CodigoError.Prohibido).ExecuteAsync(context);
            return;
        }
        await _predeterminado.HandleAsync(next, context, policy, authorizeResult);
    }
}
