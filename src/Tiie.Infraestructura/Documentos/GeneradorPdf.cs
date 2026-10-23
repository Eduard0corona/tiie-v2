using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Privacidad;

namespace Tiie.Infraestructura.Documentos;

/// <summary>Sección <c>Documentos</c> de la configuración.</summary>
public sealed class OpcionesDocumentos
{
    public const string Seccion = "Documentos";

    /// <summary>
    /// Licencia de QuestPDF que aplica a la organización: <c>Community</c>, <c>Professional</c> o <c>Enterprise</c>.
    /// No tiene valor por omisión: se decide antes de producción (docs/01#preguntas-abiertas).
    /// </summary>
    [Required(ErrorMessage = "Falta Documentos:LicenciaQuestPdf.")]
    public LicenseType? LicenciaQuestPdf { get; set; }
}

/// <summary>Comprobante de la cita y reportes en PDF con QuestPDF.</summary>
public sealed class GeneradorPdf : IGeneradorPdf
{
    private const string Guinda = "#7B1E3A";

    public GeneradorPdf(IOptions<OpcionesDocumentos> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        QuestPDF.Settings.License = opciones.Value.LicenciaQuestPdf;
    }

    public byte[] Comprobante(DatosCita cita)
    {
        ArgumentNullException.ThrowIfNull(cita);
        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter);
            pagina.Margin(1.8f, Unit.Centimetre);
            pagina.DefaultTextStyle(t => t.FontSize(10.5f));

            pagina.Header().Column(c =>
            {
                c.Item().Text("Tarjeta Inteligente de Identidad Estudiantil").FontSize(9).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Comprobante de cita").FontSize(20).Bold().FontColor(Guinda);
            });

            pagina.Content().PaddingVertical(12).Column(c =>
            {
                c.Spacing(10);
                c.Item().Background(Colors.Grey.Lighten4).Padding(10).Row(fila =>
                {
                    fila.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Folio").FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(cita.Folio).FontSize(16).Bold();
                        if (cita.FolioAnterior is not null)
                            col.Item().Text($"Folio anterior: {cita.FolioAnterior}").FontSize(9);
                    });
                    fila.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Estado").FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(Estado(cita.Estado)).FontSize(13).Bold();
                    });
                });

                Seccion(c, "Fecha y hora", $"{Capitalizar(Formatos.FechaLarga(cita.Fecha))}, {Formatos.Hora(cita.Hora)}");
                Seccion(c, "Sede", $"{cita.Modulo}\n{cita.Direccion}, {cita.Municipio}"
                    + (cita.HorarioAtencion is null ? "" : $"\nHorario: {cita.HorarioAtencion}"));
                Seccion(c, "Trámite", cita.Tramite + (cita.Costo is > 0 ? string.Create(Formatos.Cultura, $" · Costo: {cita.Costo:C}") : ""));
                Seccion(c, "Solicitante", $"{cita.NombreCompleto}\nCURP: {Enmascarar.Curp(cita.Curp)}");

                if (cita.Requisitos.Count > 0)
                {
                    c.Item().Text("Requisitos").Bold().FontColor(Guinda);
                    foreach (var requisito in cita.Requisitos)
                    {
                        c.Item().Row(fila =>
                        {
                            fila.ConstantItem(12).Text("•");
                            fila.RelativeItem().Text(requisito.NivelEscolar is null ? requisito.Texto : $"{requisito.NivelEscolar}: {requisito.Texto}");
                        });
                    }
                }
                if (!string.IsNullOrWhiteSpace(cita.Notas))
                    Seccion(c, "Notas", cita.Notas);
                if (!string.IsNullOrWhiteSpace(cita.NotasSede))
                    Seccion(c, "Indicaciones de la sede", cita.NotasSede);
            });

            pagina.Footer().Column(c =>
            {
                c.Item().Text("Presenta este comprobante y tus documentos originales. Para cancelar o reprogramar usa el enlace de tu correo.")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
                c.Item().Text("Tus datos se tratan conforme al aviso de privacidad publicado en el sitio.").FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        })).GeneratePdf();
    }

    public byte[] Reporte(DocumentoReporte reporte)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter.Landscape());
            pagina.Margin(1.2f, Unit.Centimetre);
            pagina.DefaultTextStyle(t => t.FontSize(8));

            pagina.Header().Column(c =>
            {
                c.Item().Text(reporte.Titulo).FontSize(14).Bold().FontColor(Guinda);
                foreach (var filtro in reporte.Filtros)
                    c.Item().Text(filtro).FontSize(8).FontColor(Colors.Grey.Darken2);
            });

            pagina.Content().PaddingVertical(8).Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    foreach (var _ in reporte.Columnas)
                        columnas.RelativeColumn();
                });
                tabla.Header(encabezado =>
                {
                    foreach (var columna in reporte.Columnas)
                        encabezado.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(columna).Bold();
                });
                foreach (var fila in reporte.Filas)
                    foreach (var celda in fila)
                        tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(celda);
            });

            pagina.Footer().Row(fila =>
            {
                fila.RelativeItem().Text(string.Create(Formatos.Cultura, $"Generado {reporte.GeneradoEn:yyyy-MM-dd HH:mm} UTC · {reporte.Filas.Count} filas")).FontSize(7);
                fila.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("Página ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" de ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        })).GeneratePdf();
    }

    private static void Seccion(ColumnDescriptor c, string titulo, string texto)
    {
        c.Item().Column(col =>
        {
            col.Item().Text(titulo).Bold().FontColor(Guinda);
            col.Item().Text(texto);
        });
    }

    private static string Estado(EstadoCita estado) => estado switch
    {
        EstadoCita.Agendada => "Agendada",
        EstadoCita.Atendida => "Atendida",
        EstadoCita.NoAsistio => "No asistió",
        EstadoCita.Cancelada => "Cancelada",
        EstadoCita.Reprogramada => "Reprogramada",
        _ => estado.ToString(),
    };

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], Formatos.Cultura) + texto[1..];
}
