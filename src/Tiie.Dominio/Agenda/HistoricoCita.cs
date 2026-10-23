namespace Tiie.Dominio.Agenda;

/// <summary>Total diario de citas del sistema anterior, sin datos personales.</summary>
public sealed class HistoricoCita
{
    public DateOnly Fecha { get; set; }
    public int OfertaId { get; set; }
    public Oferta Oferta { get; set; } = null!;
    public int Total { get; set; }
}
