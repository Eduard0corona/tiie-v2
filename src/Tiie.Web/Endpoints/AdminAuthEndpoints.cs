using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Privacidad;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Identidad;
using Tiie.Web.Errores;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;
using ResultadoInicio = Microsoft.AspNetCore.Identity.SignInResult;

namespace Tiie.Web.Endpoints;

/// <summary>
/// Sesión y cuenta del personal: contraseña, MFA, activación y restablecimiento (docs/07).
/// No se usa <c>MapIdentityApi</c>: expone registro público y tokens bearer.
/// </summary>
public static class AdminAuthEndpoints
{
    private const string EntidadUsuario = "Usuario";
    private const string MensajeCodigoIncorrecto = "El código no es correcto. Revisa que la hora de tu celular esté bien.";

    public static void MapAdminAuth(this RouteGroupBuilder admin)
    {
        ArgumentNullException.ThrowIfNull(admin);
        var auth = admin.MapGroup("/auth").WithTags("Administración · Sesión");

        auth.MapPost("/entrar", Entrar).AllowAnonymous().RequireRateLimiting(Limites.Entrar);
        auth.MapPost("/entrar/mfa", EntrarConCodigo).AllowAnonymous().RequireRateLimiting(Limites.Entrar);
        auth.MapPost("/entrar/recuperacion", EntrarConRecuperacion).AllowAnonymous().RequireRateLimiting(Limites.Entrar);
        auth.MapPost("/salir", Salir).RequireAuthorization(Politicas.PersonalSinMfa);
        auth.MapGet("/yo", Yo).RequireAuthorization(Politicas.PersonalSinMfa);
        auth.MapGet("/mfa", ObtenerClaveMfa).RequireAuthorization(Politicas.PersonalSinMfa);
        auth.MapPost("/mfa", ActivarMfa).RequireAuthorization(Politicas.PersonalSinMfa);
        auth.MapPost("/contrasena", CambiarContrasena).RequireAuthorization(Politicas.Personal);
        auth.MapPost("/activacion", Activar).AllowAnonymous().RequireRateLimiting(Limites.Entrar);
        auth.MapPost("/olvide-contrasena", OlvideContrasena).AllowAnonymous().RequireRateLimiting(Limites.Entrar);
        auth.MapPost("/restablecimiento", Restablecer).AllowAnonymous().RequireRateLimiting(Limites.Entrar);
    }

    private static async Task<Results<Ok<RespuestaEntrar>, ProblemHttpResult>> Entrar(
        SolicitudEntrar solicitud, SignInManager<Usuario> sesion, RegistroAuditoria auditoria, RelojAgenda reloj,
        CancellationToken ct)
    {
        Exigir.NoVacios(
            ("usuario", solicitud.Usuario, "Escribe tu usuario."),
            ("contrasena", solicitud.Contrasena, "Escribe tu contraseña."));

        var usuario = await sesion.UserManager.FindByNameAsync(solicitud.Usuario.Trim());
        var resultado = usuario is null
            ? ResultadoInicio.Failed
            : await sesion.PasswordSignInAsync(usuario, solicitud.Contrasena, isPersistent: false, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            await RegistrarInicioAsync(usuario!, sesion, auditoria, reloj, mfa: usuario!.TwoFactorEnabled);
            return TypedResults.Ok(new RespuestaEntrar(usuario.TwoFactorEnabled ? PasoSiguiente.Listo : PasoSiguiente.ConfigurarMfa));
        }
        if (resultado.RequiresTwoFactor)
            return TypedResults.Ok(new RespuestaEntrar(PasoSiguiente.CodigoMfa));

        return await RechazarAsync(usuario, resultado, "contrasena", CodigoError.CredencialesInvalidas, sesion, auditoria, ct);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> EntrarConCodigo(
        SolicitudCodigoMfa solicitud, SignInManager<Usuario> sesion, RegistroAuditoria auditoria, RelojAgenda reloj,
        CancellationToken ct)
    {
        Exigir.NoVacios(("codigo", solicitud.Codigo, "Escribe el código de tu app autenticadora."));
        var usuario = await sesion.GetTwoFactorAuthenticationUserAsync();
        if (usuario is null)
            return ErroresApi.Problema(CodigoError.NoAutenticado, "La verificación expiró. Vuelve a escribir tu contraseña.");

        var resultado = await sesion.TwoFactorAuthenticatorSignInAsync(
            CodigoAutenticador(solicitud.Codigo), isPersistent: false, rememberClient: solicitud.RecordarEsteEquipo);
        if (resultado.Succeeded)
        {
            await RegistrarInicioAsync(usuario, sesion, auditoria, reloj, mfa: true);
            return TypedResults.NoContent();
        }
        return await RechazarAsync(usuario, resultado, "mfa", CodigoError.CodigoMfaInvalido, sesion, auditoria, ct);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> EntrarConRecuperacion(
        SolicitudCodigoRecuperacion solicitud, SignInManager<Usuario> sesion, RegistroAuditoria auditoria, RelojAgenda reloj,
        CancellationToken ct)
    {
        Exigir.NoVacios(("codigoRecuperacion", solicitud.CodigoRecuperacion, "Escribe uno de tus códigos de recuperación."));
        var usuario = await sesion.GetTwoFactorAuthenticationUserAsync();
        if (usuario is null)
            return ErroresApi.Problema(CodigoError.NoAutenticado, "La verificación expiró. Vuelve a escribir tu contraseña.");

        var resultado = await sesion.TwoFactorRecoveryCodeSignInAsync(CodigoRecuperacion(solicitud.CodigoRecuperacion));
        if (resultado.Succeeded)
        {
            var restantes = await sesion.UserManager.CountRecoveryCodesAsync(usuario);
            auditoria.Agregar(AccionesAuditoria.MfaCodigoRecuperacion, EntidadUsuario, Id(usuario), new { restantes }, usuario.Id);
            await RegistrarInicioAsync(usuario, sesion, auditoria, reloj, mfa: true);
            return TypedResults.NoContent();
        }
        return await RechazarAsync(usuario, resultado, "recuperacion", CodigoError.CodigoMfaInvalido, sesion, auditoria, ct);
    }

    private static async Task<NoContent> Salir(
        SignInManager<Usuario> sesion, RegistroAuditoria auditoria, IUsuarioActual actual, CancellationToken ct)
    {
        var usuarioId = actual.UsuarioId;
        await sesion.SignOutAsync();
        await auditoria.RegistrarAsync(
            AccionesAuditoria.SesionCerrar, EntidadUsuario, usuarioId?.ToString(CultureInfo.InvariantCulture), null, usuarioId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<SesionPersonal>, ProblemHttpResult>> Yo(
        ClaimsPrincipal principal, IUsuarioActual actual, TiieDbContext db, CancellationToken ct)
    {
        var usuarioId = actual.UsuarioId;
        var usuario = await db.Users.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.Id, u.UserName, u.Nombre, u.ApellidoPaterno, u.ApellidoMaterno })
            .SingleOrDefaultAsync(ct);
        if (usuario is null)
            return ErroresApi.Problema(CodigoError.NoAutenticado);

        var esAdministrador = principal.IsInRole(Roles.Administrador);
        var modulos = await db.Modulos.AsNoTracking()
            .Where(m => m.Activo && (esAdministrador || db.UsuariosModulos.Any(um => um.UsuarioId == usuario.Id && um.ModuloId == m.ModuloId)))
            .OrderBy(m => m.Nombre)
            .Select(m => new ModuloAsignado(m.ModuloId, m.Nombre))
            .ToListAsync(ct);

        var nombreCompleto = string.Join(' ',
            new[] { usuario.Nombre, usuario.ApellidoPaterno, usuario.ApellidoMaterno }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return TypedResults.Ok(new SesionPersonal(
            usuario.Id,
            usuario.UserName ?? "",
            nombreCompleto,
            Roles.Todos.FirstOrDefault(principal.IsInRole),
            modulos,
            principal.FindFirstValue(ClaimsTiie.Mfa) == "true"));
    }

    private static async Task<Results<Ok<ClaveAutenticador>, ProblemHttpResult>> ObtenerClaveMfa(
        ClaimsPrincipal principal, SignInManager<Usuario> sesion, UrlEncoder codificador)
    {
        var usuarios = sesion.UserManager;
        var usuario = await usuarios.GetUserAsync(principal);
        if (usuario is null)
            return ErroresApi.Problema(CodigoError.NoAutenticado);
        if (usuario.TwoFactorEnabled)
            return ErroresApi.Problema(CodigoError.Prohibido, "La verificación en dos pasos ya está configurada.");

        var clave = await usuarios.GetAuthenticatorKeyAsync(usuario);
        if (string.IsNullOrEmpty(clave))
        {
            await usuarios.ResetAuthenticatorKeyAsync(usuario);
            clave = await usuarios.GetAuthenticatorKeyAsync(usuario);
            // Restablecer la clave cambia el sello de seguridad: se renueva la cookie para no cerrar la sesión.
            await sesion.RefreshSignInAsync(usuario);
        }

        var cuenta = usuario.Email ?? usuario.UserName ?? "";
        var uri = string.Create(CultureInfo.InvariantCulture,
            $"otpauth://totp/{codificador.Encode("TIIE")}:{codificador.Encode(cuenta)}?secret={clave}&issuer={codificador.Encode("TIIE")}&digits=6");
        return TypedResults.Ok(new ClaveAutenticador(FormatearClave(clave!), uri));
    }

    private static async Task<Results<Ok<RespuestaMfaActivada>, ProblemHttpResult>> ActivarMfa(
        SolicitudActivarMfa solicitud, ClaimsPrincipal principal, SignInManager<Usuario> sesion, RegistroAuditoria auditoria,
        CancellationToken ct)
    {
        Exigir.NoVacios(("codigo", solicitud.Codigo, "Escribe el código de tu app autenticadora."));
        var usuarios = sesion.UserManager;
        var usuario = await usuarios.GetUserAsync(principal);
        if (usuario is null)
            return ErroresApi.Problema(CodigoError.NoAutenticado);
        if (usuario.TwoFactorEnabled)
            return ErroresApi.Problema(CodigoError.Prohibido, "La verificación en dos pasos ya está configurada.");

        var valido = await usuarios.VerifyTwoFactorTokenAsync(
            usuario, usuarios.Options.Tokens.AuthenticatorTokenProvider, CodigoAutenticador(solicitud.Codigo));
        if (!valido)
            throw Exigir.Error("codigo", MensajeCodigoIncorrecto);

        await usuarios.SetTwoFactorEnabledAsync(usuario, true);
        var codigos = await usuarios.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10);
        await auditoria.RegistrarAsync(AccionesAuditoria.MfaConfigurar, EntidadUsuario, Id(usuario), null, usuario.Id, ct);
        // La cookie nueva ya trae mfa = true.
        await sesion.RefreshSignInAsync(usuario);
        return TypedResults.Ok(new RespuestaMfaActivada([.. codigos ?? []]));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CambiarContrasena(
        SolicitudCambioContrasena solicitud, ClaimsPrincipal principal, SignInManager<Usuario> sesion,
        RegistroAuditoria auditoria, CancellationToken ct)
    {
        Exigir.NoVacios(
            ("actual", solicitud.Actual, "Escribe tu contraseña actual."),
            ("nueva", solicitud.Nueva, "Escribe la contraseña nueva."));
        var usuario = await sesion.UserManager.GetUserAsync(principal);
        if (usuario is null)
            return ErroresApi.Problema(CodigoError.NoAutenticado);

        var resultado = await sesion.UserManager.ChangePasswordAsync(usuario, solicitud.Actual, solicitud.Nueva);
        if (!resultado.Succeeded)
            return ErroresApi.Validacion(ErroresPorCampo(resultado, e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "actual" : "nueva"));

        await auditoria.RegistrarAsync(AccionesAuditoria.ContrasenaCambiar, EntidadUsuario, Id(usuario), null, usuario.Id, ct);
        await sesion.RefreshSignInAsync(usuario);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Activar(
        SolicitudContrasenaConToken solicitud, UserManager<Usuario> usuarios, RegistroAuditoria auditoria, CancellationToken ct)
    {
        Exigir.NoVacios(("contrasena", solicitud.Contrasena, "Escribe tu contraseña."));
        var usuario = await usuarios.FindByIdAsync(solicitud.UsuarioId.ToString(CultureInfo.InvariantCulture));
        // Una cuenta ya activada no se vuelve a activar con el enlace de invitación.
        if (usuario is not { Activo: true, EmailConfirmed: false })
            return ErroresApi.Problema(CodigoError.TokenInvalido);

        var resultado = await DefinirContrasenaAsync(usuarios, usuario, solicitud);
        if (resultado is not null)
            return resultado;

        usuario.EmailConfirmed = true;
        auditoria.Agregar(AccionesAuditoria.CuentaActivar, EntidadUsuario, Id(usuario), null, usuario.Id);
        await usuarios.UpdateAsync(usuario);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted> OlvideContrasena(
        SolicitudOlvideContrasena solicitud, UserManager<Usuario> usuarios, IPlantillasCorreo plantillas, ICorreo correo,
        RegistroAuditoria auditoria, IOptions<OpcionesSitio> sitio, ILoggerFactory logs, CancellationToken ct)
    {
        // La respuesta es la misma exista o no el correo, para no revelar cuentas.
        var usuario = string.IsNullOrWhiteSpace(solicitud.Correo) ? null : await usuarios.FindByEmailAsync(solicitud.Correo.Trim());
        if (usuario is not { Activo: true, EmailConfirmed: true, Email: not null })
            return TypedResults.Accepted((string?)null);

        var token = TokenEnlace.Codificar(await usuarios.GeneratePasswordResetTokenAsync(usuario));
        var enlace = string.Create(CultureInfo.InvariantCulture,
            $"{sitio.Value.UrlBase}/admin/restablecer?usuarioId={usuario.Id}&token={token}");
        try
        {
            await correo.EnviarAsync(plantillas.Restablecimiento(usuario.Email, usuario.Nombre, enlace), ct);
        }
        catch (CorreoNoEnviadoException ex)
        {
            logs.CreateLogger(typeof(AdminAuthEndpoints)).LogWarning(ex,
                "No se pudo enviar el restablecimiento a {Correo}", Enmascarar.Correo(usuario.Email));
        }
        await auditoria.RegistrarAsync(
            AccionesAuditoria.ContrasenaSolicitarRestablecimiento, EntidadUsuario, Id(usuario), null, usuario.Id, ct);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Restablecer(
        SolicitudContrasenaConToken solicitud, UserManager<Usuario> usuarios, RegistroAuditoria auditoria, CancellationToken ct)
    {
        Exigir.NoVacios(("contrasena", solicitud.Contrasena, "Escribe tu contraseña nueva."));
        var usuario = await usuarios.FindByIdAsync(solicitud.UsuarioId.ToString(CultureInfo.InvariantCulture));
        if (usuario is not { Activo: true })
            return ErroresApi.Problema(CodigoError.TokenInvalido);

        var resultado = await DefinirContrasenaAsync(usuarios, usuario, solicitud);
        if (resultado is not null)
            return resultado;

        await usuarios.ResetAccessFailedCountAsync(usuario);
        await usuarios.SetLockoutEndDateAsync(usuario, null);
        await auditoria.RegistrarAsync(AccionesAuditoria.ContrasenaRestablecer, EntidadUsuario, Id(usuario), null, usuario.Id, ct);
        return TypedResults.NoContent();
    }

    /// <summary>Define la contraseña con el token del enlace; devuelve el error o nada si todo salió bien.</summary>
    private static async Task<ProblemHttpResult?> DefinirContrasenaAsync(
        UserManager<Usuario> usuarios, Usuario usuario, SolicitudContrasenaConToken solicitud)
    {
        var token = TokenEnlace.Decodificar(solicitud.Token);
        if (token is null)
            return ErroresApi.Problema(CodigoError.TokenInvalido);

        var resultado = await usuarios.ResetPasswordAsync(usuario, token, solicitud.Contrasena);
        if (resultado.Succeeded)
            return null;
        if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            return ErroresApi.Problema(CodigoError.TokenInvalido);
        return ErroresApi.Validacion(ErroresPorCampo(resultado, _ => "contrasena"));
    }

    private static async Task RegistrarInicioAsync(
        Usuario usuario, SignInManager<Usuario> sesion, RegistroAuditoria auditoria, RelojAgenda reloj, bool mfa)
    {
        usuario.UltimoAccesoEn = reloj.AhoraUtc();
        auditoria.Agregar(AccionesAuditoria.SesionIniciar, EntidadUsuario, Id(usuario), new { mfa }, usuario.Id);
        await sesion.UserManager.UpdateAsync(usuario);
    }

    private static async Task<ProblemHttpResult> RechazarAsync(
        Usuario? usuario, ResultadoInicio resultado, string paso, CodigoError codigo, SignInManager<Usuario> sesion,
        RegistroAuditoria auditoria, CancellationToken ct)
    {
        if (resultado.IsLockedOut && usuario is not null)
        {
            var desbloqueo = await sesion.UserManager.GetLockoutEndDateAsync(usuario);
            await auditoria.RegistrarAsync(AccionesAuditoria.SesionBloqueo, EntidadUsuario, Id(usuario), new { paso }, usuario.Id, ct);
            return ErroresApi.Problema(CodigoError.CuentaBloqueada, "Demasiados intentos fallidos.",
                [new("desbloqueoEn", desbloqueo?.ToUniversalTime())]);
        }

        // No se registra el usuario escrito: podría ser una contraseña tecleada en el campo equivocado.
        await auditoria.RegistrarAsync(
            AccionesAuditoria.SesionIntentoFallido, EntidadUsuario, usuario is null ? null : Id(usuario), new { paso }, usuario?.Id, ct);
        return ErroresApi.Problema(codigo);
    }

    private static Dictionary<string, string[]> ErroresPorCampo(IdentityResult resultado, Func<IdentityError, string> campo) =>
        resultado.Errors
            .GroupBy(campo)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

    private static string Id(Usuario usuario) => usuario.Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Código de 6 dígitos del autenticador; acepta «123 456» y «123-456».</summary>
    private static string CodigoAutenticador(string codigo) =>
        string.Concat(codigo.Where(c => !char.IsWhiteSpace(c) && c != '-'));

    /// <summary>Código de recuperación de Identity (<c>XXXXX-XXXXX</c>), sin espacios y en mayúsculas.</summary>
    private static string CodigoRecuperacion(string codigo) =>
        string.Concat(codigo.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    /// <summary>Clave en grupos de 4 en minúsculas, más fácil de teclear en la app autenticadora.</summary>
    private static string FormatearClave(string clave)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < clave.Length; i += 4)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(clave.AsSpan(i, Math.Min(4, clave.Length - i)));
        }
        return sb.ToString().ToLowerInvariant();
    }
}
