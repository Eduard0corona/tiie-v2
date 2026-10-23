using System.ComponentModel.DataAnnotations;

namespace Tiie.Aplicacion.Opciones;

/// <summary>Sección <c>Agenda</c> de la configuración. Ver docs/03-reglas-de-negocio.md#parámetros.</summary>
public sealed class OpcionesAgenda
{
    public const string Seccion = "Agenda";

    [Required]
    public string ZonaHoraria { get; set; } = "America/Mazatlan";

    /// <summary>RN-03.</summary>
    [Range(1, 120)]
    public short DiasVentanaPredeterminados { get; set; } = 49;

    /// <summary>Libres ÷ capacidad igual o menor a este valor es «Poca» disponibilidad.</summary>
    [Range(0.0, 1.0)]
    public double UmbralPocaDisponibilidad { get; set; } = 0.45;

    /// <summary>RN-13.</summary>
    [Range(0, 30)]
    public int DiasAnticipacionCambios { get; set; } = 1;

    public TimeOnly HoraGeneracionCupos { get; set; } = new(0, 15);

    /// <summary>RN-12.</summary>
    public TimeOnly HoraCierreDiario { get; set; } = new(0, 30);

    /// <summary>RN-14.</summary>
    public TimeOnly HoraRecordatorio { get; set; } = new(9, 0);

    /// <summary>Vacío: retención desactivada hasta definir el plazo.</summary>
    [Range(1, 600)]
    public int? MesesRetencion { get; set; }
}
