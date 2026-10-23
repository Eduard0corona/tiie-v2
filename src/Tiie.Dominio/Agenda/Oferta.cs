using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Comun;

namespace Tiie.Dominio.Agenda;

/// <summary>Un tipo de trámite que se atiende en un módulo. Tiene plantilla, cupos y ventana.</summary>
public sealed class Oferta : IConMarcasDeTiempo
{
    public const short DiasVentanaMinimos = 1;
    public const short DiasVentanaMaximos = 120;

    public int OfertaId { get; set; }
    public int ModuloId { get; set; }
    public Modulo Modulo { get; set; } = null!;
    public int TipoTramiteId { get; set; }
    public TipoTramite TipoTramite { get; set; } = null!;
    /// <summary>Días hacia adelante en los que se puede reservar (RN-03).</summary>
    public short DiasVentana { get; set; }
    /// <summary>Indicaciones propias de esta sede.</summary>
    public string? NotasSede { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary><c>Tramite.TramiteId</c> del sistema anterior.</summary>
    public int? IdAnterior { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ModificadoEn { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PlantillaHorario> Plantilla { get; } = [];
    public ICollection<Cupo> Cupos { get; } = [];
}
