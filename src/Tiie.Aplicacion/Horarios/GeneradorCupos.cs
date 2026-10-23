using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Agenda;

namespace Tiie.Aplicacion.Horarios;

/// <summary>Resumen de una generación de cupos, que el panel muestra al guardar una plantilla (RF-14).</summary>
public sealed record ResumenGeneracion(int CuposCreados, int CuposActualizados, int CuposEliminados, int CuposConCitasSinCambio)
{
    public static ResumenGeneracion Vacio { get; } = new(0, 0, 0, 0);

    public ResumenGeneracion Mas(ResumenGeneracion otro)
    {
        ArgumentNullException.ThrowIfNull(otro);
        return new(CuposCreados + otro.CuposCreados, CuposActualizados + otro.CuposActualizados,
            CuposEliminados + otro.CuposEliminados, CuposConCitasSinCambio + otro.CuposConCitasSinCambio);
    }
}

/// <summary>
/// Crea y ajusta los cupos de mañana a hoy + ventana a partir de la plantilla (docs/03#generación-de-cupos).
/// Es idempotente y nunca modifica cupos de hoy ni de fechas pasadas. Los cambios de capacidad son UPDATE
/// condicionales, así que conviven con reservas simultáneas.
/// </summary>
public sealed class GeneradorCupos(ITiieDb db, RelojAgenda reloj)
{
    /// <summary>Todas las ofertas activas con módulo y tipo de trámite activos.</summary>
    public async Task<ResumenGeneracion> GenerarTodasAsync(CancellationToken ct)
    {
        var ofertas = await db.Ofertas.AsNoTracking()
            .Where(o => o.Activo && o.Modulo.Activo && o.TipoTramite.Activo)
            .Select(o => o.OfertaId)
            .ToListAsync(ct);
        var total = ResumenGeneracion.Vacio;
        foreach (var ofertaId in ofertas)
            total = total.Mas(await GenerarAsync(ofertaId, ct));
        return total;
    }

    public async Task<ResumenGeneracion> GenerarAsync(int ofertaId, CancellationToken ct)
    {
        var oferta = await db.Ofertas.AsNoTracking()
            .Where(o => o.OfertaId == ofertaId)
            .Select(o => new { o.OfertaId, o.ModuloId, o.DiasVentana, Activa = o.Activo && o.Modulo.Activo && o.TipoTramite.Activo })
            .SingleOrDefaultAsync(ct);
        if (oferta is not { Activa: true })
            return ResumenGeneracion.Vacio;

        var hoy = reloj.HoyLocal();
        var desde = hoy.AddDays(1);
        var hasta = hoy.AddDays(oferta.DiasVentana);

        var plantilla = (await db.Plantillas.AsNoTracking()
                .Where(p => p.OfertaId == ofertaId)
                .Select(p => new { p.DiaSemana, p.Hora, p.Turnos })
                .ToListAsync(ct))
            .ToLookup(p => p.DiaSemana);
        var existentes = (await db.Cupos.AsNoTracking()
                .Where(c => c.OfertaId == ofertaId && c.Fecha >= desde && c.Fecha <= hasta)
                .Select(c => new { c.CupoId, c.Fecha, c.Hora, c.Capacidad, c.Ocupados })
                .ToListAsync(ct))
            .ToDictionary(c => (c.Fecha, c.Hora));
        var bloqueos = await db.Bloqueos.AsNoTracking()
            .Where(b => b.Desde <= hasta && b.Hasta >= desde)
            .Where(b => (b.ModuloId == null && b.OfertaId == null) || b.ModuloId == oferta.ModuloId || b.OfertaId == ofertaId)
            .Select(b => new { b.Desde, b.Hasta })
            .ToListAsync(ct);

        var nuevos = new List<Cupo>();
        var ajustes = new Dictionary<short, List<int>>();        // turnos de la plantilla → cupos a ajustar
        var sinHora = new List<int>();                            // cupos cuya hora ya no está en la plantilla
        var conCitasSinCambio = 0;

        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            var horas = plantilla[fecha.DayOfWeek].ToDictionary(p => p.Hora, p => (short)p.Turnos);
            foreach (var (hora, turnos) in horas)
            {
                if (!existentes.TryGetValue((fecha, hora), out var cupo))
                {
                    var cerrado = bloqueos.Any(b => b.Desde <= fecha && b.Hasta >= fecha);   // RN-07
                    nuevos.Add(new Cupo { OfertaId = ofertaId, Fecha = fecha, Hora = hora, Capacidad = turnos, Cerrado = cerrado });
                    continue;
                }
                // Con citas, la capacidad nunca baja de los lugares ocupados.
                var capacidadDeseada = Math.Max(turnos, cupo.Ocupados);
                if (cupo.Ocupados > turnos)
                    conCitasSinCambio++;
                if (cupo.Capacidad != capacidadDeseada)
                {
                    if (!ajustes.TryGetValue(turnos, out var lista))
                        ajustes[turnos] = lista = [];
                    lista.Add(cupo.CupoId);
                }
            }
            sinHora.AddRange(existentes.Values
                .Where(c => c.Fecha == fecha && !horas.ContainsKey(c.Hora))
                .Select(c => c.CupoId));
        }

        var actualizados = 0;
        foreach (var (turnos, ids) in ajustes)
        {
            actualizados += await db.Cupos
                .Where(c => ids.Contains(c.CupoId))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacidad, c => c.Ocupados > turnos ? c.Ocupados : turnos), ct);
        }

        var eliminados = 0;
        if (sinHora.Count > 0)
        {
            eliminados = await db.Cupos.Where(c => sinHora.Contains(c.CupoId) && c.Ocupados == 0).ExecuteDeleteAsync(ct);
            // Los que tienen citas no se borran: se cierran para que no acepten más reservas.
            conCitasSinCambio += await db.Cupos
                .Where(c => sinHora.Contains(c.CupoId) && c.Ocupados > 0 && !c.Cerrado)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Cerrado, true), ct);
        }

        if (nuevos.Count > 0)
        {
            db.Cupos.AddRange(nuevos);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        return new ResumenGeneracion(nuevos.Count, actualizados, eliminados, conCitasSinCambio);
    }
}
