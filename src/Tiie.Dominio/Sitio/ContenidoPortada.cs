using Tiie.Dominio.Comun;

namespace Tiie.Dominio.Sitio;

/// <summary>Cómo se destaca el aviso de la portada. Los valores se guardan como <c>tinyint</c>.</summary>
public enum TonoAviso : byte
{
    Informativo = 1,
    Importante = 2,
}

/// <summary>
/// Textos de la portada que edita el administrador: encabezado, aviso y preguntas frecuentes. Hay una sola fila;
/// mientras no exista, el sitio muestra el contenido predeterminado.
/// </summary>
public sealed class ContenidoPortada : IConMarcasDeTiempo
{
    public const byte Unico = 1;

    public byte ContenidoPortadaId { get; set; } = Unico;
    /// <summary>Etiqueta corta sobre el título.</summary>
    public required string Etiqueta { get; set; }
    public required string Titulo { get; set; }
    /// <summary>Continuación del título en el color de la marca.</summary>
    public string? TituloDestacado { get; set; }
    public required string Descripcion { get; set; }
    /// <summary>Vacío: no hay aviso.</summary>
    public string? AvisoTexto { get; set; }
    public TonoAviso AvisoTono { get; set; } = TonoAviso.Informativo;
    /// <summary>Primer día local en que se muestra; vacío: desde ya.</summary>
    public DateOnly? AvisoDesde { get; set; }
    /// <summary>Último día local en que se muestra; vacío: sin fin.</summary>
    public DateOnly? AvisoHasta { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ModificadoEn { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PreguntaFrecuente> Preguntas { get; } = [];
}

/// <summary>Pregunta frecuente de la portada. La respuesta es texto plano con enlaces (<see cref="ReglasPortada"/>).</summary>
public sealed class PreguntaFrecuente
{
    public int PreguntaFrecuenteId { get; set; }
    public byte ContenidoPortadaId { get; set; }
    public required string Pregunta { get; set; }
    public required string Respuesta { get; set; }
    public short Orden { get; set; }
}
