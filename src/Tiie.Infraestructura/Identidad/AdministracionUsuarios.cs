using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Privacidad;
using Tiie.Infraestructura.Datos;

namespace Tiie.Infraestructura.Identidad;

public sealed record UsuarioResumen(
    int UsuarioId, string Usuario, string Correo, string NombreCompleto, string? Rol, bool Activo, bool MfaActivo,
    bool CuentaActivada, DateTimeOffset? UltimoAccesoEn);

public sealed record UsuarioDetalle(
    int UsuarioId, string Usuario, string Correo, string Nombre, string ApellidoPaterno, string? ApellidoMaterno, string? Rol,
    IReadOnlyList<int> ModuloIds, bool Activo, bool MfaActivo, bool CuentaActivada, DateTimeOffset? UltimoAccesoEn,
    DateTimeOffset CreadoEn, string Version);

public sealed record CrearUsuario(
    string Usuario, string Correo, string Nombre, string ApellidoPaterno, string? ApellidoMaterno, string Rol, IReadOnlyList<int> ModuloIds);

public sealed record ActualizarUsuario(
    string Correo, string Nombre, string ApellidoPaterno, string? ApellidoMaterno, string Rol, IReadOnlyList<int> ModuloIds, string? Version);

/// <param name="InvitacionEnviada">Falso si el correo falló: la cuenta existe y la invitación se reenvía después.</param>
public sealed record UsuarioCreado(int UsuarioId, bool InvitacionEnviada);

/// <summary>
/// Usuarios del personal (RF-16). Las cuentas solo las crea un administrador; el usuario define su contraseña con
/// la invitación. Cambiar rol, módulos, activación o MFA cambia el sello de seguridad: sus sesiones dejan de valer
/// en menos de 5 minutos.
/// </summary>
public sealed class AdministracionUsuarios(
    UserManager<Usuario> usuarios,
    TiieDbContext db,
    IUsuarioActual actual,
    ICorreo correo,
    IPlantillasCorreo plantillas,
    IOptions<OpcionesSitio> sitio,
    RegistroAuditoria auditoria,
    ILogger<AdministracionUsuarios> logger)
{
    private const string EntidadUsuario = "Usuario";
    public const string MensajeInvitacionNoEnviada = "La cuenta se creó, pero la invitación no salió. Reenvíala más tarde.";

    public async Task<ListaPaginada<UsuarioResumen>> ListarAsync(
        string? busqueda, string? rol, bool? activo, int? pagina, int? tamano, CancellationToken ct)
    {
        var texto = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();
        var consulta = db.Users.AsNoTracking()
            .Where(u => texto == null || u.UserName!.Contains(texto) || u.Email!.Contains(texto)
                || u.Nombre.Contains(texto) || u.ApellidoPaterno.Contains(texto))
            .Where(u => activo == null || u.Activo == activo)
            .Where(u => rol == null || db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == rol)))
            .OrderBy(u => u.UserName)
            .Select(u => new UsuarioResumen(
                u.Id, u.UserName!, u.Email!,
                u.Nombre + " " + u.ApellidoPaterno + (u.ApellidoMaterno != null ? " " + u.ApellidoMaterno : ""),
                db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name).FirstOrDefault(),
                u.Activo, u.TwoFactorEnabled, u.EmailConfirmed, u.UltimoAccesoEn));
        return await Paginado.De(pagina, tamano).AplicarAsync(consulta, ct);
    }

    public async Task<UsuarioDetalle> ObtenerAsync(int usuarioId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == usuarioId, ct) ?? throw new NoEncontradoException();
        var rol = (await usuarios.GetRolesAsync(u)).FirstOrDefault();
        var modulos = await db.UsuariosModulos.AsNoTracking().Where(um => um.UsuarioId == usuarioId).Select(um => um.ModuloId).OrderBy(m => m).ToListAsync(ct);
        return new UsuarioDetalle(u.Id, u.UserName!, u.Email!, u.Nombre, u.ApellidoPaterno, u.ApellidoMaterno, rol, modulos,
            u.Activo, u.TwoFactorEnabled, u.EmailConfirmed, u.UltimoAccesoEn, u.CreadoEn, u.ConcurrencyStamp ?? "");
    }

    /// <summary>Crea la cuenta sin contraseña y envía la invitación. Si el correo falla, la cuenta queda creada.</summary>
    public async Task<UsuarioCreado> CrearAsync(CrearUsuario datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (string.IsNullOrWhiteSpace(datos.Usuario) || !System.Text.RegularExpressions.Regex.IsMatch(datos.Usuario, "^[A-Za-z0-9._@-]{3,50}$"))
            throw Validaciones.Error("usuario", "Usa de 3 a 50 letras, números o . _ - @");
        await ValidarAsync(datos.Correo, datos.Nombre, datos.ApellidoPaterno, datos.Rol, datos.ModuloIds, ct);

        var usuario = new Usuario
        {
            UserName = datos.Usuario.Trim(),
            Email = datos.Correo.Trim(),
            Nombre = datos.Nombre.Trim(),
            ApellidoPaterno = datos.ApellidoPaterno.Trim(),
            ApellidoMaterno = Vacio(datos.ApellidoMaterno),
            EmailConfirmed = false,
        };
        Exigir(await usuarios.CreateAsync(usuario), "usuario");
        Exigir(await usuarios.AddToRoleAsync(usuario, datos.Rol), "rol");
        await AsignarModulosAsync(usuario.Id, datos.Rol, datos.ModuloIds, ct);

        return new UsuarioCreado(usuario.Id, await EnviarInvitacionAsync(usuario, ct));
    }

    public async Task<UsuarioDetalle> ActualizarAsync(int usuarioId, ActualizarUsuario datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await ValidarAsync(datos.Correo, datos.Nombre, datos.ApellidoPaterno, datos.Rol, datos.ModuloIds, ct);
        if (string.IsNullOrWhiteSpace(datos.Version))
            throw Validaciones.Error("version", "Falta la versión del registro.");
        var usuario = await usuarios.FindByIdAsync(Id(usuarioId)) ?? throw new NoEncontradoException();
        // Versión vieja: alguien más lo modificó. Si cambia entre esta lectura y el guardado, Identity lo detecta
        // con su propio sello de concurrencia.
        if (!string.Equals(usuario.ConcurrencyStamp, datos.Version, StringComparison.Ordinal))
            throw new ReglaNegocioException(CodigoError.Conflicto);
        var rolActual = (await usuarios.GetRolesAsync(usuario)).FirstOrDefault();

        if (rolActual == Roles.Administrador && datos.Rol != Roles.Administrador)
        {
            if (actual.UsuarioId == usuarioId)
                throw Validaciones.Error("rol", "No puedes quitarte el rol de administrador.");
            await ExigirOtroAdministradorActivoAsync(usuarioId, ct);
        }

        usuario.Email = datos.Correo.Trim();
        usuario.Nombre = datos.Nombre.Trim();
        usuario.ApellidoPaterno = datos.ApellidoPaterno.Trim();
        usuario.ApellidoMaterno = Vacio(datos.ApellidoMaterno);
        await usuarios.UpdateNormalizedEmailAsync(usuario);
        var resultado = await usuarios.UpdateAsync(usuario);
        if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            throw new ReglaNegocioException(CodigoError.Conflicto);
        Exigir(resultado, "correo");

        if (rolActual != datos.Rol)
        {
            if (rolActual is not null)
                Exigir(await usuarios.RemoveFromRoleAsync(usuario, rolActual), "rol");
            Exigir(await usuarios.AddToRoleAsync(usuario, datos.Rol), "rol");
        }
        await AsignarModulosAsync(usuarioId, datos.Rol, datos.ModuloIds, ct);
        await usuarios.UpdateSecurityStampAsync(usuario);
        return await ObtenerAsync(usuarioId, ct);
    }

    /// <summary>Un administrador no se desactiva a sí mismo y siempre queda al menos uno activo.</summary>
    public async Task<UsuarioDetalle> CambiarActivoAsync(int usuarioId, bool activo, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(Id(usuarioId)) ?? throw new NoEncontradoException();
        if (!activo)
        {
            if (actual.UsuarioId == usuarioId)
                throw Validaciones.Error("activo", "No puedes desactivar tu propia cuenta.");
            if (await usuarios.IsInRoleAsync(usuario, Roles.Administrador))
                await ExigirOtroAdministradorActivoAsync(usuarioId, ct);
        }
        usuario.Activo = activo;
        Exigir(await usuarios.UpdateAsync(usuario), "activo");
        await usuarios.UpdateSecurityStampAsync(usuario);
        return await ObtenerAsync(usuarioId, ct);
    }

    public async Task ReenviarInvitacionAsync(int usuarioId, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(Id(usuarioId)) ?? throw new NoEncontradoException();
        if (usuario.EmailConfirmed)
            throw Validaciones.Error("usuarioId", "La cuenta ya está activada; usa «¿Olvidaste tu contraseña?».");
        if (!await EnviarInvitacionAsync(usuario, ct))
            throw new ReglaNegocioException(CodigoError.CorreoNoDisponible, MensajeInvitacionNoEnviada);
    }

    /// <summary>El usuario configura MFA de nuevo al entrar; sus sesiones y códigos de recuperación dejan de valer.</summary>
    public async Task RestablecerMfaAsync(int usuarioId, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(Id(usuarioId)) ?? throw new NoEncontradoException();
        Exigir(await usuarios.SetTwoFactorEnabledAsync(usuario, false), "mfa");
        Exigir(await usuarios.ResetAuthenticatorKeyAsync(usuario), "mfa");
        await usuarios.RemoveAuthenticationTokenAsync(usuario, "[AspNetUserStore]", "RecoveryCodes");
        await usuarios.UpdateSecurityStampAsync(usuario);
        await auditoria.RegistrarAsync("Mfa.Restablecer", EntidadUsuario, Id(usuarioId), null, null, ct);
    }

    /// <returns>Falso si el proveedor de correo rechazó el envío.</returns>
    private async Task<bool> EnviarInvitacionAsync(Usuario usuario, CancellationToken ct)
    {
        var token = TokenEnlace.Codificar(await usuarios.GeneratePasswordResetTokenAsync(usuario));
        var enlace = string.Create(CultureInfo.InvariantCulture, $"{sitio.Value.UrlBase}/admin/activar?usuarioId={usuario.Id}&token={token}");
        try
        {
            await correo.EnviarAsync(plantillas.Invitacion(usuario.Email!, usuario.Nombre, enlace), ct);
        }
        catch (CorreoNoEnviadoException ex)
        {
            logger.LogWarning(ex, "No se pudo enviar la invitación a {Correo}", Enmascarar.Correo(usuario.Email));
            return false;
        }
        await auditoria.RegistrarAsync("Usuario.Invitar", EntidadUsuario, Id(usuario.Id), null, null, ct);
        return true;
    }

    private async Task ValidarAsync(
        string correoUsuario, string nombre, string apellidoPaterno, string rol, IReadOnlyList<int>? moduloIds, CancellationToken ct)
    {
        var errores = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(correoUsuario) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(correoUsuario))
            errores.Add(new("correo", "El correo no es válido."));
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 80)
            errores.Add(new("nombre", "Escribe el nombre (80 caracteres o menos)."));
        if (string.IsNullOrWhiteSpace(apellidoPaterno) || apellidoPaterno.Length > 80)
            errores.Add(new("apellidoPaterno", "Escribe el primer apellido (80 caracteres o menos)."));
        if (!Roles.Todos.Contains(rol))
            errores.Add(new("rol", "El rol no existe."));
        var modulos = moduloIds ?? [];
        if (rol != Roles.Administrador && modulos.Count == 0)
            errores.Add(new("moduloIds", "Asigna al menos un módulo."));
        if (modulos.Count > 0 && await db.Modulos.CountAsync(m => modulos.Contains(m.ModuloId), ct) != modulos.Distinct().Count())
            errores.Add(new("moduloIds", "Un módulo no existe."));
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    /// <summary>Los administradores ven todos los módulos: no necesitan filas en <c>UsuarioModulo</c>.</summary>
    private async Task AsignarModulosAsync(int usuarioId, string rol, IReadOnlyList<int>? moduloIds, CancellationToken ct)
    {
        var nuevos = rol == Roles.Administrador ? [] : (moduloIds ?? []).Distinct().ToHashSet();
        var actuales = await db.UsuariosModulos.Where(um => um.UsuarioId == usuarioId).ToListAsync(ct);
        db.UsuariosModulos.RemoveRange(actuales.Where(um => !nuevos.Contains(um.ModuloId)));
        db.UsuariosModulos.AddRange(nuevos.Where(m => actuales.All(um => um.ModuloId != m))
            .Select(m => new UsuarioModulo { UsuarioId = usuarioId, ModuloId = m }));
        await db.SaveChangesAsync(ct);
    }

    private async Task ExigirOtroAdministradorActivoAsync(int usuarioId, CancellationToken ct)
    {
        var otros = await db.Users.CountAsync(u => u.Id != usuarioId && u.Activo
            && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Administrador)), ct);
        if (otros == 0)
            throw Validaciones.Error("rol", "Debe quedar al menos un administrador activo.");
    }

    private static void Exigir(IdentityResult resultado, string campo)
    {
        if (resultado.Succeeded)
            return;
        throw new ValidationException(resultado.Errors.Select(e => new ValidationFailure(
            e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail) ? "correo"
            : e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.InvalidUserName) ? "usuario"
            : campo,
            e.Description)));
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static string? Vacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
