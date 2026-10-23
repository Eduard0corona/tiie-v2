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

    private const string P = Politicas.Personal;
    private const string S = Politicas.Supervisor;
    private const string A = Politicas.Administrador;

    /// <summary>Política de cada endpoint, por «MÉTODO /ruta».</summary>
    public static IReadOnlyDictionary<string, string> Endpoints { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Sesión y cuenta
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
        ["POST /api/admin/auth/contrasena"] = P,

        // Agenda y citas
        ["GET /api/admin/agenda"] = P,
        ["GET /api/admin/citas"] = P,
        ["GET /api/admin/citas/{citaId:int}"] = P,
        ["POST /api/admin/citas"] = P,
        ["POST /api/admin/citas/{citaId:int}/asistencia"] = P,
        ["POST /api/admin/citas/{citaId:int}/correccion-asistencia"] = S,
        ["POST /api/admin/citas/{citaId:int}/cancelacion"] = P,
        ["POST /api/admin/citas/{citaId:int}/reprogramacion"] = P,
        ["POST /api/admin/citas/{citaId:int}/reenvio"] = P,
        ["GET /api/admin/citas/{citaId:int}/comprobante.pdf"] = P,
        ["GET /api/admin/ofertas/{ofertaId:int}/cupos"] = P,

        // Catálogos
        ["GET /api/admin/catalogos"] = P,
        ["GET /api/admin/modulos"] = P,
        ["POST /api/admin/modulos"] = A,
        ["GET /api/admin/modulos/{moduloId:int}"] = P,
        ["PUT /api/admin/modulos/{moduloId:int}"] = A,
        ["POST /api/admin/modulos/{moduloId:int}/activacion"] = A,
        ["GET /api/admin/tipos-tramite"] = P,
        ["POST /api/admin/tipos-tramite"] = A,
        ["GET /api/admin/tipos-tramite/{tipoTramiteId:int}"] = P,
        ["PUT /api/admin/tipos-tramite/{tipoTramiteId:int}"] = A,
        ["GET /api/admin/ofertas"] = P,
        ["GET /api/admin/ofertas/{ofertaId:int}"] = P,
        ["POST /api/admin/ofertas"] = S,
        ["PUT /api/admin/ofertas/{ofertaId:int}"] = S,

        // Horarios y bloqueos
        ["GET /api/admin/ofertas/{ofertaId:int}/plantilla"] = P,
        ["PUT /api/admin/ofertas/{ofertaId:int}/plantilla"] = S,
        ["PATCH /api/admin/cupos/{cupoId:int}"] = S,
        ["GET /api/admin/bloqueos"] = P,
        ["POST /api/admin/bloqueos"] = S,
        ["POST /api/admin/bloqueos/{bloqueoId:int}/cancelacion-citas"] = S,
        ["DELETE /api/admin/bloqueos/{bloqueoId:int}"] = S,

        // Usuarios
        ["GET /api/admin/usuarios"] = A,
        ["POST /api/admin/usuarios"] = A,
        ["GET /api/admin/usuarios/{usuarioId:int}"] = A,
        ["PUT /api/admin/usuarios/{usuarioId:int}"] = A,
        ["POST /api/admin/usuarios/{usuarioId:int}/activacion"] = A,
        ["POST /api/admin/usuarios/{usuarioId:int}/invitacion"] = A,
        ["POST /api/admin/usuarios/{usuarioId:int}/mfa/restablecimiento"] = A,

        // Reportes, tablero y auditoría
        ["GET /api/admin/reportes/citas"] = S,
        ["GET /api/admin/tablero"] = P,
        ["GET /api/admin/auditoria"] = A,
        ["GET /api/admin/procesos"] = A,
    };

    /// <summary>Roles que pasa cada política, con MFA ya configurada.</summary>
    public static IReadOnlyDictionary<string, string[]> RolesPorPolitica { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [Politicas.PersonalSinMfa] = [Roles.Administrador, Roles.Supervisor, Roles.Capturista],
        [Politicas.Personal] = [Roles.Administrador, Roles.Supervisor, Roles.Capturista],
        [Politicas.Supervisor] = [Roles.Administrador, Roles.Supervisor],
        [Politicas.Administrador] = [Roles.Administrador],
    };

    /// <summary>Ruta concreta con 1 en cada parámetro, para llamar al endpoint.</summary>
    public static string RutaConcreta(string patron) =>
        System.Text.RegularExpressions.Regex.Replace(patron, @"\{[^}]+\}", "1");
}
