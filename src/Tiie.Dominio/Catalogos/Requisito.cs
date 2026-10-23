namespace Tiie.Dominio.Catalogos;

/// <summary>Requisito de un tipo de trámite; sin nivel escolar aplica a todos.</summary>
public sealed class Requisito
{
    public int RequisitoId { get; set; }
    public int TipoTramiteId { get; set; }
    public TipoTramite TipoTramite { get; set; } = null!;
    public byte? NivelEscolarId { get; set; }
    public NivelEscolar? NivelEscolar { get; set; }
    /// <summary>Texto plano; los saltos de línea se respetan.</summary>
    public required string Texto { get; set; }
    public short Orden { get; set; }
}
