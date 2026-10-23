namespace Tiie.Dominio.Agenda;

/// <summary>
/// Lugares de una oferta en una fecha y hora. <see cref="Ocupados"/> solo cambia con un UPDATE
/// condicional dentro de la transacción de la cita (RN-02, ADR 0004).
/// </summary>
public sealed class Cupo
{
    public int CupoId { get; set; }
    public int OfertaId { get; set; }
    public Oferta Oferta { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public TimeOnly Hora { get; set; }
    public short Capacidad { get; set; }
    public short Ocupados { get; set; }
    /// <summary>Lo pone un bloqueo o el generador de cupos.</summary>
    public bool Cerrado { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
