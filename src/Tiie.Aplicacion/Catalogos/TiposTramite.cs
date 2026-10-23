using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Catalogos;

public sealed record RequisitoEntrada(byte? NivelEscolarId, string Texto);

public sealed record RequisitoDetalle(int RequisitoId, byte? NivelEscolarId, string Texto, short Orden);

/// <summary>Tipo de trámite con su lista completa de requisitos; en <c>PUT</c> reemplaza la lista y exige versión.</summary>
public sealed record DatosTipoTramite(
    string Nombre, string? Descripcion, decimal? Costo, string? Notas, IReadOnlyList<RequisitoEntrada> Requisitos,
    bool Activo = true, string? Version = null);

public sealed record TipoTramiteResumen(int TipoTramiteId, string Nombre, decimal? Costo, bool Activo, int Ofertas);

public sealed record TipoTramiteDetalle(
    int TipoTramiteId, string Nombre, string? Descripcion, decimal? Costo, string? Notas, bool Activo,
    IReadOnlyList<RequisitoDetalle> Requisitos, string Version);

public sealed class DatosTipoTramiteValidador : AbstractValidator<DatosTipoTramite>
{
    public DatosTipoTramiteValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("Escribe el nombre.").MaximumLength(120);
        RuleFor(x => x.Descripcion).MaximumLength(500);
        RuleFor(x => x.Costo).GreaterThanOrEqualTo(0).LessThan(100_000_000);
        RuleFor(x => x.Requisitos).NotNull();
        RuleForEach(x => x.Requisitos).ChildRules(r =>
            r.RuleFor(x => x.Texto).NotEmpty().WithMessage("Escribe el texto del requisito."));
    }
}

/// <summary>Tipos de trámite y requisitos por nivel escolar (RF-13). Los requisitos son texto sin límite práctico.</summary>
public sealed class TiposTramiteManejador(ITiieDb db, IValidator<DatosTipoTramite> validador)
{
    private const string IndiceNombre = "UX_TipoTramite_Nombre";

    public async Task<IReadOnlyList<TipoTramiteResumen>> ListarAsync(CancellationToken ct) =>
        await db.TiposTramite.AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoTramiteResumen(t.TipoTramiteId, t.Nombre, t.Costo, t.Activo, t.Ofertas.Count))
            .ToListAsync(ct);

    public async Task<TipoTramiteDetalle> ObtenerAsync(int tipoTramiteId, CancellationToken ct) =>
        await db.TiposTramite.AsNoTracking()
            .Where(t => t.TipoTramiteId == tipoTramiteId)
            .Select(t => new TipoTramiteDetalle(
                t.TipoTramiteId, t.Nombre, t.Descripcion, t.Costo, t.Notas, t.Activo,
                t.Requisitos.OrderBy(r => r.Orden).Select(r => new RequisitoDetalle(r.RequisitoId, r.NivelEscolarId, r.Texto, r.Orden)).ToList(),
                Versiones.ATexto(t.RowVersion)))
            .SingleOrDefaultAsync(ct) ?? throw new NoEncontradoException();

    public async Task<int> CrearAsync(DatosTipoTramite datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await ValidarAsync(datos, ct);
        var tipo = new TipoTramite { Nombre = datos.Nombre.Trim() };
        Aplicar(tipo, datos);
        db.TiposTramite.Add(tipo);
        await GuardarAsync(ct);
        return tipo.TipoTramiteId;
    }

    public async Task<TipoTramiteDetalle> ActualizarAsync(int tipoTramiteId, DatosTipoTramite datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await ValidarAsync(datos, ct);
        var version = Versiones.DeTexto(datos.Version);
        var tipo = await db.TiposTramite.Include(t => t.Requisitos).SingleOrDefaultAsync(t => t.TipoTramiteId == tipoTramiteId, ct)
            ?? throw new NoEncontradoException();
        db.Entry(tipo).Property(t => t.RowVersion).OriginalValue = version;
        // Se reemplaza la lista completa de requisitos.
        db.Requisitos.RemoveRange(tipo.Requisitos);
        tipo.Requisitos.Clear();
        Aplicar(tipo, datos);
        await GuardarAsync(ct);
        return await ObtenerAsync(tipoTramiteId, ct);
    }

    private async Task ValidarAsync(DatosTipoTramite datos, CancellationToken ct)
    {
        await validador.ValidateAndThrowAsync(datos, ct);
        var niveles = datos.Requisitos.Where(r => r.NivelEscolarId is not null).Select(r => r.NivelEscolarId!.Value).Distinct().ToList();
        if (niveles.Count > 0 && await db.NivelesEscolares.CountAsync(n => niveles.Contains(n.NivelEscolarId), ct) != niveles.Count)
            throw Validaciones.Error("requisitos", "Un nivel escolar no existe.");
    }

    private static void Aplicar(TipoTramite tipo, DatosTipoTramite d)
    {
        tipo.Nombre = d.Nombre.Trim();
        tipo.Descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim();
        tipo.Costo = d.Costo;
        tipo.Notas = string.IsNullOrWhiteSpace(d.Notas) ? null : d.Notas;
        tipo.Activo = d.Activo;
        for (var i = 0; i < d.Requisitos.Count; i++)
        {
            tipo.Requisitos.Add(new Requisito
            {
                NivelEscolarId = d.Requisitos[i].NivelEscolarId,
                Texto = d.Requisitos[i].Texto,
                Orden = (short)(i + 1),
            });
        }
    }

    private async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.ViolaIndice(IndiceNombre))
        {
            throw Validaciones.Error("nombre", "Ya existe un tipo de trámite con ese nombre.");
        }
    }
}
