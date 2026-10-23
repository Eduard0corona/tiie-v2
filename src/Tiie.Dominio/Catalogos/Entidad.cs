namespace Tiie.Dominio.Catalogos;

/// <summary>Entidad federativa. Conserva el ID del sistema anterior (25 = Sinaloa).</summary>
public sealed class Entidad
{
    public const byte Sinaloa = 25;

    public byte EntidadId { get; set; }
    public required string Nombre { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Municipio> Municipios { get; } = [];
}
