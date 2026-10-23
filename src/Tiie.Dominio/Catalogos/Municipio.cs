namespace Tiie.Dominio.Catalogos;

/// <summary>Municipio. Conserva el ID del sistema anterior.</summary>
public sealed class Municipio
{
    public short MunicipioId { get; set; }
    public byte EntidadId { get; set; }
    public Entidad Entidad { get; set; } = null!;
    public required string Nombre { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Modulo> Modulos { get; } = [];
}
