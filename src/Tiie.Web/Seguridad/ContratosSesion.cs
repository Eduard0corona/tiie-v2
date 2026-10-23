using FluentValidation;
using FluentValidation.Results;

namespace Tiie.Web.Seguridad;

public sealed record SolicitudEntrar(string Usuario, string Contrasena);

/// <summary>Qué debe hacer la SPA después de la contraseña.</summary>
public enum PasoSiguiente
{
    /// <summary>La sesión quedó iniciada con MFA (o el equipo estaba recordado).</summary>
    Listo,

    /// <summary>Pedir el código del autenticador; la sesión aún no existe.</summary>
    CodigoMfa,

    /// <summary>La sesión existe sin MFA y solo sirve para configurarla.</summary>
    ConfigurarMfa,
}

public sealed record RespuestaEntrar(PasoSiguiente SiguientePaso);

public sealed record SolicitudCodigoMfa(string Codigo, bool RecordarEsteEquipo);

public sealed record SolicitudCodigoRecuperacion(string CodigoRecuperacion);

public sealed record ModuloAsignado(int ModuloId, string Nombre);

public sealed record SesionPersonal(
    int UsuarioId,
    string Usuario,
    string NombreCompleto,
    string? Rol,
    IReadOnlyList<ModuloAsignado> Modulos,
    bool MfaActivo);

public sealed record ClaveAutenticador(string ClaveCompartida, string UriAutenticador);

public sealed record SolicitudActivarMfa(string Codigo);

public sealed record RespuestaMfaActivada(IReadOnlyList<string> CodigosRecuperacion);

public sealed record SolicitudCambioContrasena(string Actual, string Nueva);

/// <summary>Activación de cuenta o restablecimiento: el token llega en el enlace del correo.</summary>
public sealed record SolicitudContrasenaConToken(int UsuarioId, string Token, string Contrasena);

public sealed record SolicitudOlvideContrasena(string Correo);

/// <summary>Validación de formato de las solicitudes de sesión; responde 422 <c>Validacion</c>.</summary>
public static class Exigir
{
    public static void NoVacios(params (string Campo, string? Valor, string Mensaje)[] campos)
    {
        var errores = campos
            .Where(c => string.IsNullOrWhiteSpace(c.Valor))
            .Select(c => new ValidationFailure(c.Campo, c.Mensaje))
            .ToList();
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    public static ValidationException Error(string campo, string mensaje) => new([new ValidationFailure(campo, mensaje)]);
}
