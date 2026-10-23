using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Seguridad;

namespace Tiie.Aplicacion.Catalogos;

public sealed record MunicipioResumen(short MunicipioId, string Nombre);

public sealed record NivelEscolarResumen(byte NivelEscolarId, string Nombre);

/// <summary>Catálogos fijos que el panel usa en sus formularios.</summary>
public sealed record CatalogosAdmin(
    IReadOnlyList<MunicipioResumen> Municipios,
    IReadOnlyList<NivelEscolarResumen> NivelesEscolares,
    IReadOnlyList<string> Roles);

public sealed class ConsultarCatalogosAdminManejador(ITiieDb db)
{
    public async Task<CatalogosAdmin> EjecutarAsync(CancellationToken ct)
    {
        var municipios = await db.Municipios.AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new MunicipioResumen(m.MunicipioId, m.Nombre))
            .ToListAsync(ct);

        var niveles = await db.NivelesEscolares.AsNoTracking()
            .OrderBy(n => n.Orden)
            .Select(n => new NivelEscolarResumen(n.NivelEscolarId, n.Nombre))
            .ToListAsync(ct);

        return new CatalogosAdmin(municipios, niveles, Roles.Todos);
    }
}
