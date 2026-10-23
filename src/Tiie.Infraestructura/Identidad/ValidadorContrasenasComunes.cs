using Microsoft.AspNetCore.Identity;

namespace Tiie.Infraestructura.Identidad;

/// <summary>
/// Rechaza contraseñas de la lista de contraseñas frecuentes (recurso incrustado) y las que contienen
/// el usuario o la parte local del correo. La longitud mínima de 12 la valida Identity.
/// </summary>
public sealed class ValidadorContrasenasComunes : IPasswordValidator<Usuario>
{
    public const string CodigoError = "ContrasenaComun";

    private static readonly Lazy<HashSet<string>> Comunes = new(Cargar);

    public Task<IdentityResult> ValidateAsync(UserManager<Usuario> manager, Usuario user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(password))
            return Task.FromResult(IdentityResult.Success);

        var normalizada = password.Trim();
        if (Comunes.Value.Contains(normalizada) || ContieneDatosDeLaCuenta(normalizada, user))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = CodigoError,
                Description = "Esa contraseña es muy común o contiene tu usuario. Elige otra.",
            }));
        }
        return Task.FromResult(IdentityResult.Success);
    }

    /// <summary>Indica si la contraseña está en la lista incluida. Público para las pruebas.</summary>
    public static bool EsComun(string contrasena) => Comunes.Value.Contains(contrasena.Trim());

    private static bool ContieneDatosDeLaCuenta(string contrasena, Usuario usuario)
    {
        var datos = new[] { usuario.UserName, usuario.Email?.Split('@')[0] };
        return datos.Any(d => d is { Length: >= 4 } && contrasena.Contains(d, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> Cargar()
    {
        var ensamblado = typeof(ValidadorContrasenasComunes).Assembly;
        using var flujo = ensamblado.GetManifestResourceStream("Tiie.Infraestructura.Identidad.contrasenas-comunes.txt")
            ?? throw new InvalidOperationException("No se encontró la lista de contraseñas frecuentes.");
        using var lector = new StreamReader(flujo);
        var comunes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (lector.ReadLine() is { } linea)
        {
            var valor = linea.Trim();
            if (valor.Length > 0 && !valor.StartsWith('#'))
                comunes.Add(valor);
        }
        return comunes;
    }
}
