namespace Tiie.Dominio.Citas;

/// <summary>Estados de una cita. Los valores se guardan como <c>tinyint</c>.</summary>
public enum EstadoCita : byte
{
    Agendada = 1,
    Atendida = 2,
    NoAsistio = 3,
    Cancelada = 4,
    Reprogramada = 5,
}

/// <summary>Cómo se creó una cita.</summary>
public enum OrigenCita : byte
{
    Web = 1,
    Ventanilla = 2,
    Migracion = 3,
}
