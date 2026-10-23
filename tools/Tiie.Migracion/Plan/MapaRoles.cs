using System.Globalization;
using Tiie.Aplicacion.Seguridad;

namespace Tiie.Migracion.Plan;

/// <summary>
/// Valor de <c>Usuario.Tipo</c> de 2016 → rol de la versión 2, por ejemplo <c>0=Capturista,1=Supervisor,2=Administrador</c>.
/// Es obligatorio en <c>migrar</c>: la herramienta no adivina qué significa cada valor.
/// </summary>
public sealed class MapaRoles
{
    private static readonly string[] RolesValidos = [Roles.Administrador, Roles.Supervisor, Roles.Capturista];

    private readonly Dictionary<int, string> _roles;

    private MapaRoles(Dictionary<int, string> roles) => _roles = roles;

    public static MapaRoles Parsear(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var roles = new Dictionary<int, string>();
        foreach (var par in texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = par.Split('=', StringSplitOptions.TrimEntries);
            if (partes.Length != 2 || !int.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tipo))
                throw new FormatException($"«{par}» no tiene la forma <tipo>=<rol>.");
            var rol = RolesValidos.FirstOrDefault(r => string.Equals(r, partes[1], StringComparison.OrdinalIgnoreCase))
                ?? throw new FormatException($"«{partes[1]}» no es un rol. Usa {string.Join(", ", RolesValidos)}.");
            if (!roles.TryAdd(tipo, rol))
                throw new FormatException($"El tipo {tipo} aparece dos veces.");
        }
        if (roles.Count == 0)
            throw new FormatException("El mapa de roles está vacío.");
        return new MapaRoles(roles);
    }

    public string? Rol(int tipo) => _roles.GetValueOrDefault(tipo);
}
