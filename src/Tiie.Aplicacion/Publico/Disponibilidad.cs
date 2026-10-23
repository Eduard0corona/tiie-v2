using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Publico;

public sealed record DiaDisponible(DateOnly Fecha, EstadoDia Estado, int Libres, int Capacidad, string? Motivo);

public sealed record DisponibilidadOferta(int OfertaId, DateOnly Desde, DateOnly Hasta, IReadOnlyList<DiaDisponible> Dias);

public sealed record CupoDisponible(int CupoId, TimeOnly Hora, int Libres);

/// <summary>Datos de una oferta visible que necesitan la disponibilidad y los cupos.</summary>
internal sealed record OfertaReservable(int OfertaId, int ModuloId, short DiasVentana);

/// <summary>Disponibilidad por día de una oferta, recortada a la ventana (RF-02, RN-03).</summary>
public sealed class ConsultarDisponibilidadManejador(ITiieDb db, RelojAgenda reloj, IOptions<OpcionesAgenda> agenda)
{
    public async Task<DisponibilidadOferta> EjecutarAsync(int ofertaId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var oferta = await Ofertas.ExigirVisibleAsync(db, ofertaId, ct);
        var (inicio, fin) = ReglasAgenda.RecortarAVentana(desde, hasta, reloj.HoyLocal(), oferta.DiasVentana);
        if (inicio > fin)
            return new DisponibilidadOferta(ofertaId, inicio, fin, []);

        // Una sola consulta agrupada, apoyada en UX_Cupo_OfertaFechaHora; libres y capacidad solo de cupos abiertos.
        var totales = await db.Cupos.AsNoTracking()
            .Where(c => c.OfertaId == ofertaId && c.Fecha >= inicio && c.Fecha <= fin)
            .GroupBy(c => c.Fecha)
            .Select(g => new
            {
                Fecha = g.Key,
                Cupos = g.Count(),
                Abiertos = g.Count(c => !c.Cerrado),
                Capacidad = g.Sum(c => c.Cerrado ? 0 : (int)c.Capacidad),
                Ocupados = g.Sum(c => c.Cerrado ? 0 : (int)c.Ocupados),
            })
            .ToDictionaryAsync(t => t.Fecha, ct);

        var bloqueos = await Bloqueos.QueAplicanAsync(db, oferta, inicio, fin, ct);

        var dias = new List<DiaDisponible>();
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
        {
            var t = totales.TryGetValue(fecha, out var fila)
                ? new TotalesDia(fila.Cupos, fila.Abiertos, fila.Capacidad, fila.Ocupados)
                : new TotalesDia(0, 0, 0, 0);
            var estado = CalculadoraDisponibilidad.Estado(t, agenda.Value.UmbralPocaDisponibilidad);
            var motivo = estado == EstadoDia.Cerrado
                ? bloqueos.FirstOrDefault(b => b.Desde <= fecha && b.Hasta >= fecha)?.Motivo
                : null;
            dias.Add(estado == EstadoDia.Cerrado
                ? new DiaDisponible(fecha, estado, 0, 0, motivo)
                : new DiaDisponible(fecha, estado, t.Libres, t.Capacidad, null));
        }
        return new DisponibilidadOferta(ofertaId, inicio, fin, dias);
    }
}

/// <summary>Horas abiertas con lugar en una fecha de la ventana.</summary>
public sealed class ConsultarCuposManejador(ITiieDb db, RelojAgenda reloj)
{
    /// <exception cref="ReglaNegocioException"><see cref="CodigoError.FueraDeVentana"/>.</exception>
    public async Task<IReadOnlyList<CupoDisponible>> EjecutarAsync(int ofertaId, DateOnly fecha, CancellationToken ct)
    {
        var oferta = await Ofertas.ExigirVisibleAsync(db, ofertaId, ct);
        if (!ReglasAgenda.EnVentana(fecha, reloj.HoyLocal(), oferta.DiasVentana, OrigenCita.Web))
            throw new ReglaNegocioException(CodigoError.FueraDeVentana);

        return await db.Cupos.AsNoTracking()
            .Where(c => c.OfertaId == ofertaId && c.Fecha == fecha && !c.Cerrado && c.Ocupados < c.Capacidad)
            .OrderBy(c => c.Hora)
            .Select(c => new CupoDisponible(c.CupoId, c.Hora, c.Capacidad - c.Ocupados))
            .ToListAsync(ct);
    }
}

internal static class Ofertas
{
    /// <exception cref="NoEncontradoException">La oferta no existe o no está visible al público.</exception>
    public static async Task<OfertaReservable> ExigirVisibleAsync(ITiieDb db, int ofertaId, CancellationToken ct) =>
        await db.Ofertas.AsNoTracking()
            .Where(o => o.OfertaId == ofertaId)
            .Where(ConsultarCatalogoPublicoManejador.OfertaVisible)
            .Select(o => new OfertaReservable(o.OfertaId, o.ModuloId, o.DiasVentana))
            .SingleOrDefaultAsync(ct)
        ?? throw new NoEncontradoException();
}

internal sealed record BloqueoVigente(DateOnly Desde, DateOnly Hasta, string Motivo);

internal static class Bloqueos
{
    /// <summary>Bloqueos generales, del módulo o de la oferta que se cruzan con el rango (RN-07, RN-08).</summary>
    public static async Task<IReadOnlyList<BloqueoVigente>> QueAplicanAsync(
        ITiieDb db, OfertaReservable oferta, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        await db.Bloqueos.AsNoTracking()
            .Where(b => b.Desde <= hasta && b.Hasta >= desde)
            .Where(b => (b.ModuloId == null && b.OfertaId == null) || b.ModuloId == oferta.ModuloId || b.OfertaId == oferta.OfertaId)
            .OrderBy(b => b.Desde)
            .Select(b => new BloqueoVigente(b.Desde, b.Hasta, b.Motivo))
            .ToListAsync(ct);
}
