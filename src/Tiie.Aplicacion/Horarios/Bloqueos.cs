using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;

namespace Tiie.Aplicacion.Horarios;

public enum AlcanceBloqueo
{
    General,
    Modulo,
    Oferta,
}

public sealed record BloqueoResumen(
    int BloqueoId, DateOnly Desde, DateOnly Hasta, AlcanceBloqueo Alcance, int? ModuloId, string? Modulo, int? OfertaId,
    string? Oferta, string Motivo, DateTimeOffset CreadoEn);

public sealed record CrearBloqueo(DateOnly Desde, DateOnly Hasta, int? ModuloId, int? OfertaId, string Motivo);

public sealed record CitaAfectada(int CitaId, string Folio, DateOnly Fecha, TimeOnly Hora);

public sealed record BloqueoCreado(int BloqueoId, int CuposCerrados, IReadOnlyList<CitaAfectada> CitasAfectadas);

public sealed record CitasCanceladasPorBloqueo(int CitasCanceladas);

public sealed class CrearBloqueoValidador : AbstractValidator<CrearBloqueo>
{
    public CrearBloqueoValidador()
    {
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Escribe el motivo; se muestra al público.").MaximumLength(200);
        RuleFor(x => x.OfertaId).Null().When(x => x.ModuloId is not null).WithMessage("Elige un módulo o una oferta, no ambos.");
    }
}

/// <summary>
/// Bloqueos y días inhábiles (RF-15, RN-07, RN-08). Crear un bloqueo cierra los cupos de su rango y alcance pero
/// no cancela citas: se listan las afectadas y el personal decide si las cancela con aviso.
/// </summary>
public sealed class BloqueosManejador(
    ITiieDb db,
    IAutorizacionModulo alcance,
    IUsuarioActual usuario,
    RelojAgenda reloj,
    OperacionesCita operaciones,
    RegistroAuditoria auditoria,
    IValidator<CrearBloqueo> validador)
{
    public async Task<IReadOnlyList<BloqueoResumen>> ListarAsync(DateOnly? desde, DateOnly? hasta, int? moduloId, CancellationToken ct)
    {
        var permitidos = alcance.ModulosPermitidos?.ToList();
        return await db.Bloqueos.AsNoTracking()
            .Where(b => desde == null || b.Hasta >= desde)
            .Where(b => hasta == null || b.Desde <= hasta)
            .Where(b => moduloId == null || b.ModuloId == moduloId || (b.Oferta != null && b.Oferta.ModuloId == moduloId))
            .Where(b => permitidos == null
                || (b.ModuloId == null && b.OfertaId == null)
                || (b.ModuloId != null && permitidos.Contains(b.ModuloId.Value))
                || (b.Oferta != null && permitidos.Contains(b.Oferta.ModuloId)))
            .OrderByDescending(b => b.Desde)
            .Select(b => new BloqueoResumen(
                b.BloqueoId, b.Desde, b.Hasta,
                b.ModuloId != null ? AlcanceBloqueo.Modulo : b.OfertaId != null ? AlcanceBloqueo.Oferta : AlcanceBloqueo.General,
                b.ModuloId ?? (b.Oferta != null ? b.Oferta.ModuloId : null),
                b.Modulo != null ? b.Modulo.Nombre : b.Oferta != null ? b.Oferta.Modulo.Nombre : null,
                b.OfertaId,
                b.Oferta != null ? b.Oferta.TipoTramite.Nombre : null,
                b.Motivo, b.CreadoEn))
            .ToListAsync(ct);
    }

    public async Task<BloqueoCreado> CrearAsync(CrearBloqueo datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await validador.ValidateAndThrowAsync(datos, ct);
        if (datos.ModuloId is { } moduloId && !await db.Modulos.AnyAsync(m => m.ModuloId == moduloId, ct))
            throw Validaciones.Error("moduloId", "El módulo no existe.");
        await ExigirPermisoAsync(datos.ModuloId, datos.OfertaId, ct);

        var bloqueo = new Bloqueo
        {
            Desde = datos.Desde,
            Hasta = datos.Hasta,
            ModuloId = datos.ModuloId,
            OfertaId = datos.OfertaId,
            Motivo = datos.Motivo.Trim(),
            CreadoPor = usuario.UsuarioId ?? throw new ReglaNegocioException(CodigoError.NoAutenticado),
        };

        var cerrados = await db.EnTransaccionAsync(async ct2 =>
        {
            db.Bloqueos.Add(bloqueo);
            await db.SaveChangesAsync(ct2);
            return await CuposDe(bloqueo).Where(c => !c.Cerrado)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Cerrado, true), ct2);
        }, ct);

        return new BloqueoCreado(bloqueo.BloqueoId, cerrados, await CitasAfectadasAsync(bloqueo, ct));
    }

    /// <summary>Cancela las citas Agendada del bloqueo y avisa a cada persona con «Cierre por bloqueo».</summary>
    public async Task<CitasCanceladasPorBloqueo> CancelarCitasAsync(int bloqueoId, string? motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 300)
            throw Validaciones.Error("motivo", "Escribe el motivo (300 caracteres o menos).");
        var bloqueo = await ExigirAsync(bloqueoId, ct);

        var canceladas = new List<string>();
        foreach (var afectada in await CitasAfectadasAsync(bloqueo, ct))
        {
            var cita = await ConsultasCita.ExigirAsync(db, afectada.CitaId, ct);
            try
            {
                await operaciones.CancelarAsync(cita, motivo.Trim(), usuario.UsuarioId, TipoNotificacion.CierrePorBloqueo, ct);
                canceladas.Add(cita.Folio);
            }
            catch (ReglaNegocioException ex) when (ex.Codigo == CodigoError.CitaNoModificable)
            {
                // Cambió de estado mientras tanto (por ejemplo, el ciudadano la canceló): se omite.
            }
        }
        await auditoria.RegistrarAsync("Bloqueo.CancelarCitas", "Bloqueo", bloqueoId.ToString(CultureInfo.InvariantCulture),
            new { motivo, folios = canceladas }, null, ct);
        return new CitasCanceladasPorBloqueo(canceladas.Count);
    }

    /// <summary>Elimina el bloqueo y reabre los cupos de su rango que no cubra otro bloqueo.</summary>
    public async Task EliminarAsync(int bloqueoId, CancellationToken ct)
    {
        var bloqueo = await ExigirAsync(bloqueoId, ct);
        await db.EnTransaccionAsync(async ct2 =>
        {
            await db.Bloqueos.Where(b => b.BloqueoId == bloqueoId).ExecuteDeleteAsync(ct2);
            await CuposDe(bloqueo)
                .Where(c => c.Cerrado)
                .Where(c => !db.Bloqueos.Any(b => b.Desde <= c.Fecha && b.Hasta >= c.Fecha
                    && ((b.ModuloId == null && b.OfertaId == null) || b.ModuloId == c.Oferta.ModuloId || b.OfertaId == c.OfertaId)))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Cerrado, false), ct2);
            return true;
        }, ct);
        await auditoria.RegistrarAsync("Bloqueo.Eliminar", "Bloqueo", bloqueoId.ToString(CultureInfo.InvariantCulture),
            new { bloqueo.Desde, bloqueo.Hasta, bloqueo.ModuloId, bloqueo.OfertaId, bloqueo.Motivo }, null, ct);
    }

    private IQueryable<Cupo> CuposDe(Bloqueo b) =>
        db.Cupos.Where(c => c.Fecha >= b.Desde && c.Fecha <= b.Hasta)
            .Where(c => (b.ModuloId == null && b.OfertaId == null) || c.Oferta.ModuloId == b.ModuloId || c.OfertaId == b.OfertaId);

    private async Task<IReadOnlyList<CitaAfectada>> CitasAfectadasAsync(Bloqueo b, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal();
        var permitidos = alcance.ModulosPermitidos?.ToList();
        return await db.Citas.AsNoTracking()
            .Where(c => c.Estado == EstadoCita.Agendada && c.Cupo.Fecha >= b.Desde && c.Cupo.Fecha <= b.Hasta && c.Cupo.Fecha >= hoy)
            .Where(c => (b.ModuloId == null && b.OfertaId == null) || c.Cupo.Oferta.ModuloId == b.ModuloId || c.Cupo.OfertaId == b.OfertaId)
            .Where(c => permitidos == null || permitidos.Contains(c.Cupo.Oferta.ModuloId))
            .OrderBy(c => c.Cupo.Fecha).ThenBy(c => c.Cupo.Hora)
            .Select(c => new CitaAfectada(c.CitaId, c.Folio, c.Cupo.Fecha, c.Cupo.Hora))
            .ToListAsync(ct);
    }

    private async Task<Bloqueo> ExigirAsync(int bloqueoId, CancellationToken ct)
    {
        var bloqueo = await db.Bloqueos.AsNoTracking().SingleOrDefaultAsync(b => b.BloqueoId == bloqueoId, ct)
            ?? throw new NoEncontradoException();
        await ExigirPermisoAsync(bloqueo.ModuloId, bloqueo.OfertaId, ct);
        return bloqueo;
    }

    /// <summary>Un bloqueo general (día inhábil) es solo del administrador; uno de módulo u oferta, del supervisor del módulo.</summary>
    private async Task ExigirPermisoAsync(int? moduloId, int? ofertaId, CancellationToken ct)
    {
        if (moduloId is { } m)
            alcance.ExigirAcceso(m);
        else if (ofertaId is { } o)
            alcance.ExigirAcceso(await Catalogos.OfertasManejador.ModuloDeAsync(db, o, ct));
        else
            alcance.ExigirAdministrador();
    }
}
