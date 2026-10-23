using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Citas;

namespace Tiie.Aplicacion.Reportes;

public enum FormatoReporte
{
    Json,
    Xlsx,
    Pdf,
}

public sealed record FiltroReporte(DateOnly Desde, DateOnly Hasta, int? ModuloId, int? TipoTramiteId, EstadoCita? Estado);

public sealed record FilaReporte(
    string Folio, DateOnly Fecha, TimeOnly Hora, string Modulo, string Tramite, EstadoCita Estado, OrigenCita Origen);

public sealed record TotalEstado(EstadoCita Estado, int Citas);

/// <summary>Reporte de citas por rango inclusivo, sin datos personales (RF-17).</summary>
public sealed record ReporteCitas(
    FiltroReporte Filtro, IReadOnlyList<FilaReporte> Filas, IReadOnlyList<TotalEstado> Totales, bool Truncado);

public sealed record ArchivoReporte(byte[] Contenido, string TipoContenido, string Nombre);

/// <summary>Reportes con las mismas filas en pantalla, Excel y PDF. Un supervisor solo ve sus módulos.</summary>
public sealed class ReportesManejador(
    ITiieDb db, IAutorizacionModulo alcance, IGeneradorExcel excel, IGeneradorPdf pdf, RelojAgenda reloj)
{
    public const int FilasMaximas = 20_000;

    public async Task<ReporteCitas> CitasAsync(FiltroReporte filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (filtro.Hasta < filtro.Desde)
            throw Validaciones.Error("hasta", "La fecha final no puede ser anterior a la inicial.");
        if (filtro.Hasta.DayNumber - filtro.Desde.DayNumber > 366)
            throw Validaciones.Error("hasta", "El rango no puede pasar de un año.");
        if (filtro.ModuloId is { } m)
            alcance.ExigirAcceso(m);

        // Rango inclusivo: el día «hasta» cuenta completo.
        var consulta = db.Citas.AsNoTracking()
            .SoloModulosPermitidos(alcance, c => c.Cupo.Oferta.ModuloId)
            .Where(c => c.Cupo.Fecha >= filtro.Desde && c.Cupo.Fecha <= filtro.Hasta)
            .Where(c => filtro.ModuloId == null || c.Cupo.Oferta.ModuloId == filtro.ModuloId)
            .Where(c => filtro.TipoTramiteId == null || c.Cupo.Oferta.TipoTramiteId == filtro.TipoTramiteId)
            .Where(c => filtro.Estado == null || c.Estado == filtro.Estado);

        var totales = await consulta.GroupBy(c => c.Estado)
            .Select(g => new TotalEstado(g.Key, g.Count()))
            .ToListAsync(ct);
        var filas = await consulta
            .OrderBy(c => c.Cupo.Fecha).ThenBy(c => c.Cupo.Hora).ThenBy(c => c.Folio)
            .Take(FilasMaximas + 1)
            .Select(c => new FilaReporte(c.Folio, c.Cupo.Fecha, c.Cupo.Hora, c.Cupo.Oferta.Modulo.Nombre,
                c.Cupo.Oferta.TipoTramite.Nombre, c.Estado, c.Origen))
            .ToListAsync(ct);
        var truncado = filas.Count > FilasMaximas;
        return new ReporteCitas(filtro, [.. filas.Take(FilasMaximas)], [.. totales.OrderBy(t => t.Estado)], truncado);
    }

    public async Task<ArchivoReporte> ExportarAsync(FiltroReporte filtro, FormatoReporte formato, CancellationToken ct)
    {
        var reporte = await CitasAsync(filtro, ct);
        var documento = new DocumentoReporte(
            "Reporte de citas",
            [
                $"Del {Formatos.FechaCorta(filtro.Desde)} al {Formatos.FechaCorta(filtro.Hasta)}",
                string.Join(" · ", reporte.Totales.Select(t => string.Create(CultureInfo.InvariantCulture, $"{Estado(t.Estado)}: {t.Citas}"))),
                reporte.Truncado ? string.Create(CultureInfo.InvariantCulture, $"Solo se incluyen las primeras {FilasMaximas} filas.") : "",
            ],
            ["Folio", "Fecha", "Hora", "Módulo", "Trámite", "Estado", "Origen"],
            [.. reporte.Filas.Select(f => (IReadOnlyList<string>)[f.Folio, Formatos.FechaCorta(f.Fecha), Formatos.Hora(f.Hora), f.Modulo, f.Tramite, Estado(f.Estado), f.Origen.ToString()])],
            reloj.AhoraUtc());
        var nombre = string.Create(CultureInfo.InvariantCulture, $"citas-{filtro.Desde:yyyyMMdd}-{filtro.Hasta:yyyyMMdd}");
        return formato == FormatoReporte.Pdf
            ? new ArchivoReporte(pdf.Reporte(documento), "application/pdf", nombre + ".pdf")
            : new ArchivoReporte(excel.Reporte(documento), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre + ".xlsx");
    }

    private static string Estado(EstadoCita estado) => estado == EstadoCita.NoAsistio ? "No asistió" : estado.ToString();
}

public sealed record DiaOcupacion(DateOnly Fecha, int Capacidad, int Ocupados);

public sealed record InasistenciaModulo(int ModuloId, string Modulo, int Citas, int Inasistencias, double Porcentaje);

/// <summary>Indicadores del tablero (RF-18).</summary>
public sealed record Tablero(
    IReadOnlyList<TotalEstado> Hoy, IReadOnlyList<DiaOcupacion> Proximos7Dias, IReadOnlyList<InasistenciaModulo> Inasistencia30Dias);

public sealed class TableroManejador(ITiieDb db, IAutorizacionModulo alcance, RelojAgenda reloj)
{
    public async Task<Tablero> EjecutarAsync(int? moduloId, CancellationToken ct)
    {
        if (moduloId is { } m)
            alcance.ExigirAcceso(m);
        var hoy = reloj.HoyLocal();

        var citas = db.Citas.AsNoTracking()
            .SoloModulosPermitidos(alcance, c => c.Cupo.Oferta.ModuloId)
            .Where(c => moduloId == null || c.Cupo.Oferta.ModuloId == moduloId);

        var deHoy = await citas.Where(c => c.Cupo.Fecha == hoy && c.Estado != EstadoCita.Reprogramada)
            .GroupBy(c => c.Estado)
            .Select(g => new TotalEstado(g.Key, g.Count()))
            .ToListAsync(ct);

        var manana = hoy.AddDays(1);
        var finSemana = hoy.AddDays(7);
        var ocupacion = await db.Cupos.AsNoTracking()
            .SoloModulosPermitidos(alcance, c => c.Oferta.ModuloId)
            .Where(c => moduloId == null || c.Oferta.ModuloId == moduloId)
            .Where(c => c.Fecha >= manana && c.Fecha <= finSemana && !c.Cerrado)
            .GroupBy(c => c.Fecha)
            .Select(g => new DiaOcupacion(g.Key, g.Sum(c => (int)c.Capacidad), g.Sum(c => (int)c.Ocupados)))
            .ToListAsync(ct);

        var hace30 = hoy.AddDays(-30);
        var asistencia = await citas
            .Where(c => c.Cupo.Fecha >= hace30 && c.Cupo.Fecha < hoy)
            .Where(c => c.Estado == EstadoCita.Atendida || c.Estado == EstadoCita.NoAsistio)
            .GroupBy(c => new { c.Cupo.Oferta.ModuloId, c.Cupo.Oferta.Modulo.Nombre })
            .Select(g => new { g.Key.ModuloId, g.Key.Nombre, Citas = g.Count(), Inasistencias = g.Count(c => c.Estado == EstadoCita.NoAsistio) })
            .ToListAsync(ct);

        return new Tablero(
            [.. deHoy.OrderBy(t => t.Estado)],
            [.. ocupacion.OrderBy(o => o.Fecha)],
            [.. asistencia.OrderBy(a => a.Nombre).Select(a => new InasistenciaModulo(
                a.ModuloId, a.Nombre, a.Citas, a.Inasistencias, Math.Round(100.0 * a.Inasistencias / a.Citas, 1)))]);
    }
}
