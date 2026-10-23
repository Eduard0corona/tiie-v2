using ClosedXML.Excel;
using Tiie.Aplicacion.Puertos;

namespace Tiie.Infraestructura.Documentos;

/// <summary>Reportes en Excel con ClosedXML: las mismas filas que la pantalla (RF-17).</summary>
public sealed class GeneradorExcel : IGeneradorExcel
{
    public byte[] Reporte(DocumentoReporte reporte)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Reporte");

        hoja.Cell(1, 1).Value = reporte.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        var fila = 2;
        foreach (var filtro in reporte.Filtros)
            hoja.Cell(fila++, 1).Value = filtro;
        fila++;

        var encabezado = fila;
        for (var c = 0; c < reporte.Columnas.Count; c++)
        {
            hoja.Cell(encabezado, c + 1).Value = reporte.Columnas[c];
            hoja.Cell(encabezado, c + 1).Style.Font.Bold = true;
            hoja.Cell(encabezado, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEEEEE");
        }
        foreach (var datos in reporte.Filas)
        {
            fila++;
            for (var c = 0; c < datos.Count; c++)
                // Texto siempre: CURP y folios no deben convertirse a número ni a fecha.
                hoja.Cell(fila, c + 1).SetValue(datos[c]);
        }
        if (reporte.Filas.Count > 0)
            hoja.Range(encabezado, 1, fila, reporte.Columnas.Count).SetAutoFilter();
        hoja.Columns().AdjustToContents(encabezado, fila, 8, 60);
        hoja.SheetView.FreezeRows(encabezado);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
