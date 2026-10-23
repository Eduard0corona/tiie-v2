using System.Globalization;
using System.Security.Claims;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;

namespace Tiie.Web.Seguridad;

/// <summary>Usuario e IP de la petición actual, a partir de la sesión del personal.</summary>
public sealed class UsuarioActual(IHttpContextAccessor accesor) : IUsuarioActual
{
    public int? UsuarioId =>
        accesor.HttpContext?.User is { Identity.IsAuthenticated: true } usuario
        && int.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    public string? Ip => accesor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>
/// Alcance por módulo con los claims <c>modulo</c> de la sesión; no consulta la base en cada petición.
/// Los administradores tienen todos los módulos.
/// </summary>
public sealed class AutorizacionModulo(IHttpContextAccessor accesor) : IAutorizacionModulo
{
    private static readonly IReadOnlySet<int> Ninguno = new HashSet<int>();

    private IReadOnlySet<int>? _modulos;
    private bool _calculado;

    public IReadOnlySet<int>? ModulosPermitidos
    {
        get
        {
            if (!_calculado)
            {
                _modulos = Calcular(accesor.HttpContext?.User);
                _calculado = true;
            }
            return _modulos;
        }
    }

    public bool PuedeAcceder(int moduloId) => ModulosPermitidos is null || ModulosPermitidos.Contains(moduloId);

    public void ExigirAcceso(int moduloId)
    {
        if (!PuedeAcceder(moduloId))
            throw new NoEncontradoException();
    }

    internal static IReadOnlySet<int>? Calcular(ClaimsPrincipal? usuario)
    {
        if (usuario is not { Identity.IsAuthenticated: true })
            return Ninguno;
        if (usuario.IsInRole(Roles.Administrador))
            return null;
        return usuario.FindAll(ClaimsTiie.Modulo)
            .Select(c => int.TryParse(c.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (int?)null)
            .OfType<int>()
            .ToHashSet();
    }
}
