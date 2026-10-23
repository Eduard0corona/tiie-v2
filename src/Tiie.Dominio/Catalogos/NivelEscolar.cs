namespace Tiie.Dominio.Catalogos;

/// <summary>Agrupa requisitos: básico, o medio superior y superior.</summary>
public sealed class NivelEscolar
{
    public const byte Basico = 1;
    public const byte MedioSuperiorYSuperior = 2;

    public byte NivelEscolarId { get; set; }
    public required string Nombre { get; set; }
    public byte Orden { get; set; }
}
