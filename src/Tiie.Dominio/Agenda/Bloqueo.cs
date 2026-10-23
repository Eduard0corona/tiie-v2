using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Comun;

namespace Tiie.Dominio.Agenda;

/// <summary>
/// Cierre por rango de fechas inclusivo (RN-07, RN-08). Sin módulo ni oferta es general: un día inhábil.
/// </summary>
public sealed class Bloqueo : IConFechaCreacion
{
    public int BloqueoId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int? ModuloId { get; set; }
    public Modulo? Modulo { get; set; }
    public int? OfertaId { get; set; }
    public Oferta? Oferta { get; set; }
    /// <summary>Se muestra al ciudadano como «Cerrado: motivo».</summary>
    public required string Motivo { get; set; }
    /// <summary>Usuario del personal que lo creó.</summary>
    public int CreadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
