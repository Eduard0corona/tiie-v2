using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tiie.Dominio.Citas;
using Tiie.Infraestructura.Datos;
using Tiie.Migracion.Origen;
using Tiie.Migracion.Plan;
using Tiie.Migracion.Reportes;

namespace Tiie.Migracion.Destino;

/// <param name="Origen">Conteo directo en la base anterior.</param>
/// <param name="Esperado">Lo que debe haber en la versión 2 después de aplicar las reglas y los reportes.</param>
/// <param name="Explicacion">Por qué el origen y lo esperado pueden diferir, y en qué reporte se ve.</param>
public sealed record Verificacion(string Nombre, long Origen, long Esperado, long Destino, string Explicacion)
{
    public bool Correcta => Esperado == Destino;
}

/// <summary>Las verificaciones de docs/14#conciliación. Una diferencia sin explicar detiene el corte.</summary>
public sealed class Conciliador(TiieDbContext db)
{
    public async Task<IReadOnlyList<Verificacion>> ConciliarAsync(DatosOrigen origen, PlanMigracion plan, DateOnly corte, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(origen);
        ArgumentNullException.ThrowIfNull(plan);
        var v = new List<Verificacion>();

        v.Add(new("Módulos activos",
            origen.Modulos.Count(m => m.Activo),
            plan.Modulos.Count(m => m.Activo),
            await db.Modulos.CountAsync(m => m.IdAnterior != null && m.Activo, ct),
            "Módulos con municipio inexistente (modulos.csv)"));

        v.Add(new("Ofertas",
            plan.Ofertas.Sum(o => o.Tramites.Count),
            plan.Ofertas.Count,
            await db.Ofertas.CountAsync(o => o.IdAnterior != null, ct),
            "Trámites del mismo tipo en un módulo se fusionan (ofertas-fusionadas.csv)"));

        // Horas de lunes por oferta: cada horario activo del trámite es una hora de lunes a viernes.
        var ofertas = await db.Ofertas.Where(o => o.IdAnterior != null).ToDictionaryAsync(o => o.IdAnterior!.Value, o => o.OfertaId, ct);
        var horasDestino = await db.Plantillas.Where(p => p.DiaSemana == DayOfWeek.Monday)
            .GroupBy(p => p.OfertaId).Select(g => new { g.Key, Horas = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Horas, ct);
        var horasPlan = plan.Plantillas.Where(p => p.Dia == DayOfWeek.Monday).GroupBy(p => p.OfertaIdAnterior).ToDictionary(g => g.Key, g => g.Count());
        var distintas = horasPlan.Count(h => !ofertas.TryGetValue(h.Key, out var id) || horasDestino.GetValueOrDefault(id) != h.Value);
        v.Add(new("Ofertas con horas de lunes distintas a los horarios activos",
            0, 0, distintas,
            "Una plantilla editada en el panel después de migrar también cuenta aquí: revísala"));

        var migradas = db.Citas.Where(c => c.FolioAnterior != null && c.Estado == EstadoCita.Agendada && c.Cupo.Fecha >= corte);
        v.Add(new("Citas futuras migradas",
            plan.CitasFuturasOrigen,
            plan.CitasAMigrar,
            await migradas.CountAsync(ct),
            "Excepciones y CURP repetidas sin decisión o descartadas (excepciones.csv, duplicados-curp.csv)"));

        var cuposFuturos = db.Cupos.Where(c => c.Fecha >= corte);
        v.Add(new("Suma de Ocupados en cupos futuros",
            plan.CitasAMigrar,
            await db.Citas.CountAsync(c => c.Estado == EstadoCita.Agendada && c.Cupo.Fecha >= corte, ct),
            await cuposFuturos.SumAsync(c => (int)c.Ocupados, ct),
            "Esperado: las citas Agendadas futuras que hay en la versión 2 (migradas o reservadas después)"));

        v.Add(new("CURP distintas con cita migrada",
            origen.CitasVigentes.Where(c => c.FechaCita >= corte && !string.IsNullOrWhiteSpace(c.Curp))
                .Select(c => Tiie.Dominio.Solicitantes.Curp.Normalizar(c.Curp!)).Distinct().Count(),
            plan.Cupos.SelectMany(c => c.Citas).Where(c => c.Curp is not null).Select(c => c.Curp).Distinct().Count(),
            await migradas.Where(c => c.Solicitante.Curp != null).Select(c => c.Solicitante.Curp).Distinct().CountAsync(ct),
            "CURP inválidas que no miden 18 y citas no migradas (curps-invalidas.csv, excepciones.csv)"));

        var historicoDestino = (await db.HistoricoCitas.Select(h => new { h.Fecha, h.Total }).ToListAsync(ct))
            .GroupBy(h => Mes(h.Fecha)).ToDictionary(g => g.Key, g => g.Sum(h => h.Total));
        var historicoPlan = plan.Historicos.GroupBy(h => Mes(h.Fecha)).ToDictionary(g => g.Key, g => g.Sum(h => h.Total));
        var mesesDistintos = historicoPlan.Keys.Union(historicoDestino.Keys)
            .Count(m => historicoPlan.GetValueOrDefault(m) != historicoDestino.GetValueOrDefault(m));
        v.Add(new("Meses con total histórico distinto", 0, 0, mesesDistintos,
            $"Origen: {origen.Historicos.Sum(h => h.Total)} citas pasadas; sin oferta: {origen.Historicos.Sum(h => h.Total) - plan.Historicos.Sum(h => h.Total)} (historico-sin-oferta.csv)"));

        var usuariosPlan = plan.Usuarios.Select(u => u.Usuario.Usuario.Trim().ToUpperInvariant()).ToList();
        v.Add(new("Usuarios activos",
            origen.Usuarios.Count(u => u.Activo),
            plan.Usuarios.Count,
            await db.Users.CountAsync(u => usuariosPlan.Contains(u.NormalizedUserName!), ct),
            "Sin correo, con tipo sin rol o rechazados por Identity (usuarios.csv, rechazos-destino.csv)"));

        return v;
    }

    public static Reporte Reporte(IEnumerable<Verificacion> verificaciones)
    {
        var reporte = new Reporte("conciliacion.csv", "Verificacion", "Origen", "Esperado", "Destino", "Resultado", "Explicacion");
        foreach (var v in verificaciones ?? [])
            reporte.Agregar(v.Nombre, Texto(v.Origen), Texto(v.Esperado), Texto(v.Destino), v.Correcta ? "Correcta" : "DIFERENCIA", v.Explicacion);
        return reporte;
    }

    private static string Mes(DateOnly fecha) => fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string Texto(long valor) => valor.ToString(CultureInfo.InvariantCulture);
}
