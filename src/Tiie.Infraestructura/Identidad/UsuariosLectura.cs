using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Infraestructura.Datos;

namespace Tiie.Infraestructura.Identidad;

public sealed class UsuariosLectura(TiieDbContext db) : IUsuariosLectura
{
    public async Task<IReadOnlyDictionary<int, string>> NombresAsync(IEnumerable<int> usuarioIds, CancellationToken ct)
    {
        var ids = usuarioIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, string>();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "", ct);
    }
}
