using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Publico;

public sealed record TipoTramitePublico(
    int TipoTramiteId, string Nombre, string? Descripcion, decimal? Costo, IReadOnlyList<RequisitoTexto> Requisitos);

public sealed record MunicipioPublico(short MunicipioId, string Nombre);

public sealed record ModuloPublico(
    int ModuloId, string Nombre, short MunicipioId, string Municipio, string Direccion, string? HorarioAtencion,
    decimal? Latitud, decimal? Longitud);

public sealed record OfertaPublica(int OfertaId, int TipoTramiteId, string Nombre, decimal? Costo, string? NotasSede);

/// <summary>
/// Catálogo del sitio público (RF-01). Un módulo, tipo de trámite u oferta inactivos no aparecen en ningún punto.
/// </summary>
public sealed class ConsultarCatalogoPublicoManejador(ITiieDb db)
{
    /// <summary>Ofertas que el público puede reservar: activas, con módulo y tipo activos.</summary>
    internal static readonly Expression<Func<Oferta, bool>> OfertaVisible =
        o => o.Activo && o.Modulo.Activo && o.TipoTramite.Activo;

    public async Task<IReadOnlyList<TipoTramitePublico>> TiposTramiteAsync(CancellationToken ct) =>
        await db.TiposTramite.AsNoTracking()
            .Where(t => t.Activo && t.Ofertas.Any(o => o.Activo && o.Modulo.Activo))
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoTramitePublico(
                t.TipoTramiteId, t.Nombre, t.Descripcion, t.Costo,
                t.Requisitos
                    .OrderBy(r => r.NivelEscolarId ?? 0).ThenBy(r => r.Orden)
                    .Select(r => new RequisitoTexto(r.NivelEscolar != null ? r.NivelEscolar.Nombre : null, r.Texto))
                    .ToList()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MunicipioPublico>> MunicipiosAsync(CancellationToken ct) =>
        await db.Municipios.AsNoTracking()
            .Where(m => m.Modulos.Any(mo => mo.Ofertas.AsQueryable().Any(OfertaVisible)))
            .OrderBy(m => m.Nombre)
            .Select(m => new MunicipioPublico(m.MunicipioId, m.Nombre))
            .ToListAsync(ct);

    /// <summary>Sedes con al menos una oferta visible; todas o las de un municipio.</summary>
    public async Task<IReadOnlyList<ModuloPublico>> ModulosAsync(short? municipioId, CancellationToken ct)
    {
        var filas = await db.Modulos.AsNoTracking()
            .Where(m => municipioId == null || m.MunicipioId == municipioId)
            .Where(m => m.Ofertas.AsQueryable().Any(OfertaVisible))
            .OrderBy(m => m.Municipio.Nombre).ThenBy(m => m.Nombre)
            .Select(m => new
            {
                m.ModuloId,
                m.Nombre,
                m.MunicipioId,
                Municipio = m.Municipio.Nombre,
                m.Calle,
                m.NumeroExterior,
                m.NumeroInterior,
                m.Colonia,
                m.CodigoPostal,
                m.HorarioAtencion,
                m.Latitud,
                m.Longitud,
            })
            .ToListAsync(ct);
        return [.. filas.Select(m => new ModuloPublico(
            m.ModuloId, m.Nombre, m.MunicipioId, m.Municipio,
            Formatos.Direccion(m.Calle, m.NumeroExterior, m.NumeroInterior, m.Colonia, m.CodigoPostal),
            m.HorarioAtencion, m.Latitud, m.Longitud))];
    }

    /// <exception cref="NoEncontradoException">El módulo no existe o está inactivo.</exception>
    public async Task<IReadOnlyList<OfertaPublica>> OfertasAsync(int moduloId, CancellationToken ct)
    {
        if (!await db.Modulos.AnyAsync(m => m.ModuloId == moduloId && m.Activo, ct))
            throw new NoEncontradoException();

        return await db.Ofertas.AsNoTracking()
            .Where(o => o.ModuloId == moduloId)
            .Where(OfertaVisible)
            .OrderBy(o => o.TipoTramite.Nombre)
            .Select(o => new OfertaPublica(o.OfertaId, o.TipoTramiteId, o.TipoTramite.Nombre, o.TipoTramite.Costo, o.NotasSede))
            .ToListAsync(ct);
    }
}
