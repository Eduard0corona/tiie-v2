using Tiie.Dominio.Agenda;
using Tiie.Dominio.Comun;

namespace Tiie.Dominio.Catalogos;

/// <summary>Sede física de atención.</summary>
public sealed class Modulo : IConMarcasDeTiempo
{
    public const string SinNumero = "S/N";

    public int ModuloId { get; set; }
    public short MunicipioId { get; set; }
    public Municipio Municipio { get; set; } = null!;
    public required string Nombre { get; set; }
    public required string Calle { get; set; }
    public string NumeroExterior { get; set; } = SinNumero;
    public string? NumeroInterior { get; set; }
    public required string Colonia { get; set; }
    public required string CodigoPostal { get; set; }
    public string? Localidad { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    /// <summary>Texto informativo, por ejemplo «Lunes a viernes de 8:00 a 15:00».</summary>
    public string? HorarioAtencion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary><c>Modulo.ModuloId</c> del sistema anterior.</summary>
    public int? IdAnterior { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ModificadoEn { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Oferta> Ofertas { get; } = [];
}
