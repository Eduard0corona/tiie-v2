using Tiie.Aplicacion.Seguridad;
using Tiie.Web.Seguridad;

namespace Tiie.Integracion.Tests.Autorizacion;

/// <summary>
/// Matriz esperada de <c>/api/admin</c> (docs/07#matriz-de-permisos). Agregar un endpoint sin declararlo
/// aquí hace fallar <see cref="AutorizacionTests"/>.
/// </summary>
public static class MatrizPermisos
{
    public const string Anonimo = "Anonimo";

    /// <summary>Política de cada endpoint, por «MÉTODO /ruta».</summary>
    public static IReadOnlyDictionary<string, string> Endpoints { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["POST /api/admin/auth/entrar"] = Anonimo,
        ["POST /api/admin/auth/entrar/mfa"] = Anonimo,
        ["POST /api/admin/auth/entrar/recuperacion"] = Anonimo,
        ["POST /api/admin/auth/activacion"] = Anonimo,
        ["POST /api/admin/auth/olvide-contrasena"] = Anonimo,
        ["POST /api/admin/auth/restablecimiento"] = Anonimo,
        ["POST /api/admin/auth/salir"] = Politicas.PersonalSinMfa,
        ["GET /api/admin/auth/yo"] = Politicas.PersonalSinMfa,
        ["GET /api/admin/auth/mfa"] = Politicas.PersonalSinMfa,
        ["POST /api/admin/auth/mfa"] = Politicas.PersonalSinMfa,
        ["POST /api/admin/auth/contrasena"] = Politicas.Personal,
        ["GET /api/admin/catalogos"] = Politicas.Personal,
        ["GET /api/admin/procesos"] = Politicas.Administrador,
    };

    /// <summary>Roles que pasa cada política, con MFA ya configurada.</summary>
    public static IReadOnlyDictionary<string, string[]> RolesPorPolitica { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [Politicas.PersonalSinMfa] = [Roles.Administrador, Roles.Supervisor, Roles.Capturista],
        [Politicas.Personal] = [Roles.Administrador, Roles.Supervisor, Roles.Capturista],
        [Politicas.Supervisor] = [Roles.Administrador, Roles.Supervisor],
        [Politicas.Administrador] = [Roles.Administrador],
    };
}
