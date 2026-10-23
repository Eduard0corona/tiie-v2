using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Catalogos;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Horarios;

public sealed record HoraPlantilla(TimeOnly Hora, byte Turnos);

/// <summary>Un día de la semana: 0 = domingo … 6 = sábado.</summary>
public sealed record DiaPlantilla(byte DiaSemana, IReadOnlyList<HoraPlantilla> Horas);

public sealed record PlantillaSemanal(IReadOnlyList<DiaPlantilla> Dias);

/// <summary>Plantilla semanal de una oferta (RF-14, RN-04 a RN-06). Guardarla regenera los cupos futuros.</summary>
public sealed class PlantillasManejador(
    ITiieDb db, IAutorizacionModulo alcance, GeneradorCupos generador, RegistroAuditoria auditoria)
{
    public async Task<PlantillaSemanal> ObtenerAsync(int ofertaId, CancellationToken ct)
    {
        alcance.ExigirAcceso(await OfertasManejador.ModuloDeAsync(db, ofertaId, ct));
        var filas = await db.Plantillas.AsNoTracking()
            .Where(p => p.OfertaId == ofertaId)
            .Select(p => new { p.DiaSemana, p.Hora, p.Turnos })
            .ToListAsync(ct);
        return new PlantillaSemanal([.. filas
            .GroupBy(f => f.DiaSemana)
            .OrderBy(g => g.Key)
            .Select(g => new DiaPlantilla((byte)g.Key, [.. g.OrderBy(f => f.Hora).Select(f => new HoraPlantilla(f.Hora, f.Turnos))]))]);
    }

    /// <summary>Reemplaza la semana completa y regenera los cupos de mañana al final de la ventana.</summary>
    public async Task<ResumenGeneracion> GuardarAsync(int ofertaId, PlantillaSemanal plantilla, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plantilla);
        alcance.ExigirAcceso(await OfertasManejador.ModuloDeAsync(db, ofertaId, ct));
        Validar(plantilla);

        var filas = plantilla.Dias.SelectMany(d => d.Horas.Select(h => new PlantillaHorario
        {
            OfertaId = ofertaId,
            DiaSemana = (DayOfWeek)d.DiaSemana,
            Hora = new TimeOnly(h.Hora.Hour, h.Hora.Minute),
            Turnos = h.Turnos,
        })).ToList();

        await db.EnTransaccionAsync(async ct2 =>
        {
            await db.Plantillas.Where(p => p.OfertaId == ofertaId).ExecuteDeleteAsync(ct2);
            db.Plantillas.AddRange(filas);
            await db.SaveChangesAsync(ct2);
            return true;
        }, ct);

        var resumen = await generador.GenerarAsync(ofertaId, ct);
        await auditoria.RegistrarAsync("Plantilla.Guardar", "Oferta", ofertaId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new { horas = filas.Count, turnos = filas.Sum(f => f.Turnos), resumen }, null, ct);
        return resumen;
    }

    private static void Validar(PlantillaSemanal plantilla)
    {
        if (plantilla.Dias.Any(d => d.DiaSemana > 6))
            throw Validaciones.Error("dias", "El día de la semana va de 0 (domingo) a 6 (sábado).");
        if (plantilla.Dias.GroupBy(d => d.DiaSemana).Any(g => g.Count() > 1))
            throw Validaciones.Error("dias", "Cada día de la semana aparece una sola vez.");
        foreach (var dia in plantilla.Dias)
        {
            if (dia.Horas.Any(h => h.Turnos == 0))
                throw Validaciones.Error("dias", "Cada hora necesita al menos un turno.");
            if (dia.Horas.GroupBy(h => new TimeOnly(h.Hora.Hour, h.Hora.Minute)).Any(g => g.Count() > 1))
                throw Validaciones.Error("dias", "Una hora aparece dos veces en el mismo día.");
        }
    }
}

public sealed record CupoAdmin(int CupoId, DateOnly Fecha, TimeOnly Hora, short Capacidad, short Ocupados, bool Cerrado, string Version);

public sealed record AjusteCupo(short? Capacidad, bool? Cerrado, string? Version);

/// <summary>Consulta y ajuste de cupos por el personal (RF-15).</summary>
public sealed class CuposManejador(
    ITiieDb db, IAutorizacionModulo alcance, Reloj.RelojAgenda reloj, RegistroAuditoria auditoria)
{
    /// <summary>Cupos de una oferta; por omisión de hoy al final de la ventana (el personal también ve hoy).</summary>
    public async Task<IReadOnlyList<CupoAdmin>> ListarAsync(int ofertaId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        alcance.ExigirAcceso(await OfertasManejador.ModuloDeAsync(db, ofertaId, ct));
        var hoy = reloj.HoyLocal();
        var dias = await db.Ofertas.Where(o => o.OfertaId == ofertaId).Select(o => o.DiasVentana).SingleAsync(ct);
        var inicio = desde ?? hoy;
        var fin = hasta ?? hoy.AddDays(dias);
        if (fin.DayNumber - inicio.DayNumber > 366)
            throw Validaciones.Error("hasta", "El rango no puede pasar de un año.");

        return await db.Cupos.AsNoTracking()
            .Where(c => c.OfertaId == ofertaId && c.Fecha >= inicio && c.Fecha <= fin)
            .OrderBy(c => c.Fecha).ThenBy(c => c.Hora)
            .Select(c => new CupoAdmin(c.CupoId, c.Fecha, c.Hora, c.Capacidad, c.Ocupados, c.Cerrado, Versiones.ATexto(c.RowVersion)))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Cambia capacidad o cierre con un UPDATE condicional: la capacidad nunca queda por debajo de los lugares
    /// ocupados, aunque entre una reserva en ese instante (RN-02).
    /// </summary>
    public async Task<CupoAdmin> AjustarAsync(int cupoId, AjusteCupo ajuste, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ajuste);
        var cupo = await db.Cupos.AsNoTracking()
            .Where(c => c.CupoId == cupoId)
            .Select(c => new { c.Oferta.ModuloId, c.Capacidad, c.Cerrado })
            .SingleOrDefaultAsync(ct) ?? throw new NoEncontradoException();
        alcance.ExigirAcceso(cupo.ModuloId);
        var version = Versiones.DeTexto(ajuste.Version);
        if (ajuste.Capacidad is <= 0)
            throw Validaciones.Error("capacidad", "La capacidad debe ser mayor que cero.");

        var capacidad = ajuste.Capacidad ?? cupo.Capacidad;
        var cerrado = ajuste.Cerrado ?? cupo.Cerrado;
        var filas = await db.Cupos
            .Where(c => c.CupoId == cupoId && c.RowVersion == version && c.Ocupados <= capacidad)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacidad, capacidad).SetProperty(c => c.Cerrado, cerrado), ct);
        if (filas == 0)
        {
            var actual = await db.Cupos.AsNoTracking().Where(c => c.CupoId == cupoId).Select(c => new { c.RowVersion, c.Ocupados }).SingleAsync(ct);
            if (!actual.RowVersion.SequenceEqual(version))
                throw new Dominio.Errores.ReglaNegocioException(Dominio.Errores.CodigoError.Conflicto);
            throw Validaciones.Error("capacidad", $"La capacidad no puede ser menor que los {actual.Ocupados} lugares ocupados.");
        }

        await auditoria.RegistrarAsync("Cupo.Ajustar", "Cupo", cupoId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new { capacidad = new { anterior = cupo.Capacidad, nuevo = capacidad }, cerrado = new { anterior = cupo.Cerrado, nuevo = cerrado } }, null, ct);
        return await db.Cupos.AsNoTracking()
            .Where(c => c.CupoId == cupoId)
            .Select(c => new CupoAdmin(c.CupoId, c.Fecha, c.Hora, c.Capacidad, c.Ocupados, c.Cerrado, Versiones.ATexto(c.RowVersion)))
            .SingleAsync(ct);
    }
}
