using Tiie.Aplicacion.Citas;

namespace Tiie.Aplicacion.Puertos;

/// <summary>Documentos PDF: el comprobante de la cita y los reportes.</summary>
public interface IGeneradorPdf
{
    /// <summary>Comprobante con folio, fecha, hora, sede, trámite, requisitos y notas (RF-04).</summary>
    byte[] Comprobante(DatosCita cita);

    /// <summary>Reporte tabular con título, filtros aplicados y filas (RF-17).</summary>
    byte[] Reporte(DocumentoReporte reporte);
}

/// <summary>Hojas de cálculo de los reportes (RF-17).</summary>
public interface IGeneradorExcel
{
    byte[] Reporte(DocumentoReporte reporte);
}

/// <summary>Contenido de un reporte, independiente del formato de salida.</summary>
public sealed record DocumentoReporte(
    string Titulo,
    IReadOnlyList<string> Filtros,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    DateTimeOffset GeneradoEn);
