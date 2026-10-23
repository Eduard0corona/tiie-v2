using Tiie.Dominio.Agenda;
using Tiie.Dominio.Comun;

namespace Tiie.Dominio.Catalogos;

/// <summary>Trámite del catálogo: primera vez, sustitución, reactivación…</summary>
public sealed class TipoTramite : IConMarcasDeTiempo
{
    public int TipoTramiteId { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    /// <summary>Solo informativo: el pago no se hace en línea.</summary>
    public decimal? Costo { get; set; }
    /// <summary>Se imprimen en el comprobante.</summary>
    public string? Notas { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ModificadoEn { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Requisito> Requisitos { get; } = [];
    public ICollection<Oferta> Ofertas { get; } = [];
}
