namespace Tiie.Dominio.Agenda;

/// <summary>Estado de un día en el calendario público (docs/03#disponibilidad-por-día).</summary>
public enum EstadoDia
{
    Alta,
    Poca,
    Llena,
    Cerrado,
    SinServicio,
}

/// <summary>Totales de los cupos de una oferta en un día.</summary>
/// <param name="Cupos">Cupos generados ese día, abiertos o cerrados.</param>
/// <param name="CuposAbiertos">Cupos que aceptan reservas.</param>
/// <param name="Capacidad">Suma de la capacidad de los cupos abiertos.</param>
/// <param name="Ocupados">Suma de los lugares ocupados en los cupos abiertos.</param>
public sealed record TotalesDia(int Cupos, int CuposAbiertos, int Capacidad, int Ocupados)
{
    public int Libres => Math.Max(0, Capacidad - Ocupados);
}

public static class CalculadoraDisponibilidad
{
    /// <summary>
    /// Sin cupos: <see cref="EstadoDia.SinServicio"/>. Todos cerrados: <see cref="EstadoDia.Cerrado"/>.
    /// Sin lugares libres: <see cref="EstadoDia.Llena"/>. Libres ÷ capacidad hasta el umbral:
    /// <see cref="EstadoDia.Poca"/>. Si no, <see cref="EstadoDia.Alta"/>.
    /// </summary>
    public static EstadoDia Estado(TotalesDia totales, double umbralPoca)
    {
        ArgumentNullException.ThrowIfNull(totales);
        if (totales.Cupos == 0)
            return EstadoDia.SinServicio;
        if (totales.CuposAbiertos == 0)
            return EstadoDia.Cerrado;
        if (totales.Libres == 0)
            return EstadoDia.Llena;
        return (double)totales.Libres / totales.Capacidad <= umbralPoca ? EstadoDia.Poca : EstadoDia.Alta;
    }
}
