namespace Tiie.Dominio.Agenda;

/// <summary>Una hora de la plantilla semanal de una oferta y sus turnos (RN-04 a RN-06).</summary>
public sealed class PlantillaHorario
{
    public int PlantillaHorarioId { get; set; }
    public int OfertaId { get; set; }
    public Oferta Oferta { get; set; } = null!;
    /// <summary>0 = domingo … 6 = sábado, igual que <see cref="DayOfWeek"/>.</summary>
    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly Hora { get; set; }
    public byte Turnos { get; set; }
}
