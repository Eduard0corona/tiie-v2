using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Horarios;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Catalogos;

public sealed record OfertaAdmin(
    int OfertaId, int ModuloId, string Modulo, int TipoTramiteId, string Tramite, short DiasVentana, string? NotasSede,
    bool Activo, string Version);

public sealed record CrearOferta(int ModuloId, int TipoTramiteId, short? DiasVentana, string? NotasSede);

public sealed record ActualizarOferta(short DiasVentana, string? NotasSede, bool Activo, string? Version);

/// <summary>Ofertas: un tipo de trámite en un módulo, con su ventana de 1 a 120 días (RF-13, RN-03).</summary>
public sealed class OfertasManejador(
    ITiieDb db, IAutorizacionModulo alcance, GeneradorCupos generador, IOptions<OpcionesAgenda> agenda)
{
    private const string IndiceModuloTipo = "UX_Oferta_ModuloTipo";

    public async Task<IReadOnlyList<OfertaAdmin>> ListarAsync(int? moduloId, CancellationToken ct) =>
        await Proyectar(db.Ofertas.AsNoTracking()
                .SoloModulosPermitidos(alcance, o => o.ModuloId)
                .Where(o => moduloId == null || o.ModuloId == moduloId)
                .OrderBy(o => o.Modulo.Nombre).ThenBy(o => o.TipoTramite.Nombre))
            .ToListAsync(ct);

    public async Task<OfertaAdmin> ObtenerAsync(int ofertaId, CancellationToken ct)
    {
        alcance.ExigirAcceso(await ModuloDeAsync(db, ofertaId, ct));
        return await Proyectar(db.Ofertas.AsNoTracking().Where(o => o.OfertaId == ofertaId)).SingleAsync(ct);
    }

    public async Task<int> CrearAsync(CrearOferta datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        alcance.ExigirAcceso(datos.ModuloId);
        var dias = datos.DiasVentana ?? agenda.Value.DiasVentanaPredeterminados;
        ValidarDias(dias);
        if (!await db.Modulos.AnyAsync(m => m.ModuloId == datos.ModuloId, ct))
            throw Validaciones.Error("moduloId", "El módulo no existe.");
        if (!await db.TiposTramite.AnyAsync(t => t.TipoTramiteId == datos.TipoTramiteId, ct))
            throw Validaciones.Error("tipoTramiteId", "El tipo de trámite no existe.");

        var oferta = new Oferta
        {
            ModuloId = datos.ModuloId,
            TipoTramiteId = datos.TipoTramiteId,
            DiasVentana = dias,
            NotasSede = string.IsNullOrWhiteSpace(datos.NotasSede) ? null : datos.NotasSede,
        };
        db.Ofertas.Add(oferta);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.ViolaIndice(IndiceModuloTipo))
        {
            throw Validaciones.Error("tipoTramiteId", "Ese trámite ya se ofrece en el módulo.");
        }
        await generador.GenerarAsync(oferta.OfertaId, ct);
        return oferta.OfertaId;
    }

    public async Task<OfertaAdmin> ActualizarAsync(int ofertaId, ActualizarOferta datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        alcance.ExigirAcceso(await ModuloDeAsync(db, ofertaId, ct));
        ValidarDias(datos.DiasVentana);
        var version = Versiones.DeTexto(datos.Version);
        var oferta = await db.Ofertas.SingleAsync(o => o.OfertaId == ofertaId, ct);
        db.Entry(oferta).Property(o => o.RowVersion).OriginalValue = version;
        oferta.DiasVentana = datos.DiasVentana;
        oferta.NotasSede = string.IsNullOrWhiteSpace(datos.NotasSede) ? null : datos.NotasSede;
        oferta.Activo = datos.Activo;
        await db.SaveChangesAsync(ct);
        await generador.GenerarAsync(ofertaId, ct);
        return await ObtenerAsync(ofertaId, ct);
    }

    /// <exception cref="NoEncontradoException">La oferta no existe.</exception>
    internal static async Task<int> ModuloDeAsync(ITiieDb db, int ofertaId, CancellationToken ct) =>
        await db.Ofertas.Where(o => o.OfertaId == ofertaId).Select(o => (int?)o.ModuloId).SingleOrDefaultAsync(ct)
        ?? throw new NoEncontradoException();

    private static void ValidarDias(short dias)
    {
        if (dias is < Oferta.DiasVentanaMinimos or > Oferta.DiasVentanaMaximos)
            throw Validaciones.Error("diasVentana", "La ventana va de 1 a 120 días.");
    }

    private static IQueryable<OfertaAdmin> Proyectar(IQueryable<Oferta> consulta) =>
        consulta.Select(o => new OfertaAdmin(
            o.OfertaId, o.ModuloId, o.Modulo.Nombre, o.TipoTramiteId, o.TipoTramite.Nombre, o.DiasVentana, o.NotasSede,
            o.Activo, Versiones.ATexto(o.RowVersion)));
}
