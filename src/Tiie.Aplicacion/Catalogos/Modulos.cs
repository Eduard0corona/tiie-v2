using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Catalogos;

public sealed record ModuloResumen(int ModuloId, string Nombre, short MunicipioId, string Municipio, bool Activo);

public sealed record ModuloDetalle(
    int ModuloId, short MunicipioId, string Nombre, string Calle, string NumeroExterior, string? NumeroInterior,
    string Colonia, string CodigoPostal, string? Localidad, string? Telefono, string? Correo, string? HorarioAtencion,
    decimal? Latitud, decimal? Longitud, bool Activo, string Version);

/// <summary>Datos editables de un módulo; en <c>PUT</c> viajan con <see cref="Version"/>.</summary>
public sealed record DatosModulo(
    short MunicipioId, string Nombre, string Calle, string? NumeroExterior, string? NumeroInterior, string Colonia,
    string CodigoPostal, string? Localidad, string? Telefono, string? Correo, string? HorarioAtencion,
    decimal? Latitud, decimal? Longitud, string? Version = null);

public sealed record ResultadoActivacionModulo(bool Activo, int CitasFuturas);

public sealed class DatosModuloValidador : AbstractValidator<DatosModulo>
{
    public DatosModuloValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("Escribe el nombre.").MaximumLength(120);
        RuleFor(x => x.Calle).NotEmpty().WithMessage("Escribe la calle.").MaximumLength(100);
        RuleFor(x => x.NumeroExterior).MaximumLength(15);
        RuleFor(x => x.NumeroInterior).MaximumLength(15);
        RuleFor(x => x.Colonia).NotEmpty().WithMessage("Escribe la colonia.").MaximumLength(80);
        RuleFor(x => x.CodigoPostal).Matches("^[0-9]{5}$").WithMessage("Escribe 5 dígitos.");
        RuleFor(x => x.Localidad).MaximumLength(80);
        RuleFor(x => x.Telefono).Matches("^[0-9]{10}$").WithMessage("Escribe 10 dígitos.").When(x => !string.IsNullOrEmpty(x.Telefono));
        RuleFor(x => x.Correo).EmailAddress().WithMessage("El correo no es válido.").MaximumLength(150).When(x => !string.IsNullOrEmpty(x.Correo));
        RuleFor(x => x.HorarioAtencion).MaximumLength(255);
        RuleFor(x => x.Latitud).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitud).InclusiveBetween(-180m, 180m);
    }
}

/// <summary>Módulos (RF-12). Solo el administrador los crea y edita; el resto ve los suyos.</summary>
public sealed class ModulosManejador(
    ITiieDb db, IAutorizacionModulo alcance, RelojAgenda reloj, IValidator<DatosModulo> validador)
{
    public async Task<IReadOnlyList<ModuloResumen>> ListarAsync(bool? activo, CancellationToken ct) =>
        await db.Modulos.AsNoTracking()
            .SoloModulosPermitidos(alcance, m => m.ModuloId)
            .Where(m => activo == null || m.Activo == activo)
            .OrderBy(m => m.Municipio.Nombre).ThenBy(m => m.Nombre)
            .Select(m => new ModuloResumen(m.ModuloId, m.Nombre, m.MunicipioId, m.Municipio.Nombre, m.Activo))
            .ToListAsync(ct);

    public async Task<ModuloDetalle> ObtenerAsync(int moduloId, CancellationToken ct)
    {
        alcance.ExigirAcceso(moduloId);
        return await db.Modulos.AsNoTracking()
            .Where(m => m.ModuloId == moduloId)
            .Select(m => new ModuloDetalle(
                m.ModuloId, m.MunicipioId, m.Nombre, m.Calle, m.NumeroExterior, m.NumeroInterior, m.Colonia, m.CodigoPostal,
                m.Localidad, m.Telefono, m.Correo, m.HorarioAtencion, m.Latitud, m.Longitud, m.Activo, Versiones.ATexto(m.RowVersion)))
            .SingleOrDefaultAsync(ct) ?? throw new NoEncontradoException();
    }

    public async Task<int> CrearAsync(DatosModulo datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await validador.ValidateAndThrowAsync(datos, ct);
        await ExigirMunicipioAsync(datos.MunicipioId, ct);
        var modulo = new Modulo { Nombre = "", Calle = "", Colonia = "", CodigoPostal = "" };
        Aplicar(modulo, datos);
        db.Modulos.Add(modulo);
        await db.SaveChangesAsync(ct);
        return modulo.ModuloId;
    }

    public async Task<ModuloDetalle> ActualizarAsync(int moduloId, DatosModulo datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await validador.ValidateAndThrowAsync(datos, ct);
        var version = Versiones.DeTexto(datos.Version);
        await ExigirMunicipioAsync(datos.MunicipioId, ct);
        var modulo = await db.Modulos.SingleOrDefaultAsync(m => m.ModuloId == moduloId, ct) ?? throw new NoEncontradoException();
        db.Entry(modulo).Property(m => m.RowVersion).OriginalValue = version;
        Aplicar(modulo, datos);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(moduloId, ct);
    }

    /// <summary>
    /// RN-11: un módulo con historia no se borra, se desactiva. Deja de ofrecerse y se informa cuántas citas
    /// futuras tiene para que el personal decida qué hacer con ellas.
    /// </summary>
    public async Task<ResultadoActivacionModulo> CambiarActivoAsync(int moduloId, bool activo, CancellationToken ct)
    {
        var modulo = await db.Modulos.SingleOrDefaultAsync(m => m.ModuloId == moduloId, ct) ?? throw new NoEncontradoException();
        modulo.Activo = activo;
        await db.SaveChangesAsync(ct);
        var hoy = reloj.HoyLocal();
        var futuras = await db.Citas.CountAsync(c =>
            c.Cupo.Oferta.ModuloId == moduloId && c.Estado == EstadoCita.Agendada && c.Cupo.Fecha >= hoy, ct);
        return new ResultadoActivacionModulo(activo, futuras);
    }

    private async Task ExigirMunicipioAsync(short municipioId, CancellationToken ct)
    {
        if (!await db.Municipios.AnyAsync(m => m.MunicipioId == municipioId, ct))
            throw Validaciones.Error("municipioId", "El municipio no existe.");
    }

    private static void Aplicar(Modulo modulo, DatosModulo d)
    {
        modulo.MunicipioId = d.MunicipioId;
        modulo.Nombre = d.Nombre.Trim();
        modulo.Calle = d.Calle.Trim();
        modulo.NumeroExterior = string.IsNullOrWhiteSpace(d.NumeroExterior) ? Modulo.SinNumero : d.NumeroExterior.Trim();
        modulo.NumeroInterior = Vacio(d.NumeroInterior);
        modulo.Colonia = d.Colonia.Trim();
        modulo.CodigoPostal = d.CodigoPostal;
        modulo.Localidad = Vacio(d.Localidad);
        modulo.Telefono = Vacio(d.Telefono);
        modulo.Correo = Vacio(d.Correo);
        modulo.HorarioAtencion = Vacio(d.HorarioAtencion);
        modulo.Latitud = d.Latitud;
        modulo.Longitud = d.Longitud;
    }

    private static string? Vacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
