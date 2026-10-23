using Tiie.Dominio.Errores;

namespace Tiie.Dominio.Citas;

/// <summary>Transiciones de estado de la cita (docs/03#estados-de-la-cita).</summary>
public static class ReglasCita
{
    /// <summary>RN-12: el supervisor corrige Atendida ↔ No asistió durante estos días después de la cita.</summary>
    public const int DiasCorreccionAsistencia = 7;

    /// <summary>RN-13: último día en que el ciudadano puede cancelar o reprogramar.</summary>
    public static DateOnly ModificableHasta(DateOnly fechaCita, int diasAnticipacion) => fechaCita.AddDays(-diasAnticipacion);

    /// <summary>RN-13: con <paramref name="diasAnticipacion"/> = 1, el último día es el anterior a la cita.</summary>
    public static bool CiudadanoPuedeModificar(EstadoCita estado, DateOnly fechaCita, DateOnly hoy, int diasAnticipacion) =>
        estado == EstadoCita.Agendada && hoy <= ModificableHasta(fechaCita, diasAnticipacion);

    /// <exception cref="ReglaNegocioException"><see cref="CodigoError.CitaNoModificable"/>.</exception>
    public static void ExigirModificablePorCiudadano(EstadoCita estado, DateOnly fechaCita, DateOnly hoy, int diasAnticipacion)
    {
        if (!CiudadanoPuedeModificar(estado, fechaCita, hoy, diasAnticipacion))
            throw new ReglaNegocioException(CodigoError.CitaNoModificable);
    }

    /// <summary>El personal no tiene el plazo de RN-13: puede cancelar o reprogramar hasta la hora de la cita.</summary>
    public static bool PersonalPuedeModificar(EstadoCita estado, DateOnly fecha, TimeOnly hora, DateTime ahoraLocal) =>
        estado == EstadoCita.Agendada && ahoraLocal < fecha.ToDateTime(hora);

    /// <exception cref="ReglaNegocioException"><see cref="CodigoError.CitaNoModificable"/>.</exception>
    public static void ExigirModificablePorPersonal(EstadoCita estado, DateOnly fecha, TimeOnly hora, DateTime ahoraLocal)
    {
        if (!PersonalPuedeModificar(estado, fecha, hora, ahoraLocal))
            throw new ReglaNegocioException(CodigoError.CitaNoModificable);
    }

    /// <summary>RF-09: la asistencia se registra en citas Agendada de hoy o de días anteriores, nunca futuras.</summary>
    /// <exception cref="ReglaNegocioException"><see cref="CodigoError.CitaNoModificable"/>.</exception>
    public static void ExigirAsistenciaRegistrable(EstadoCita estado, DateOnly fechaCita, DateOnly hoy)
    {
        if (estado != EstadoCita.Agendada)
            throw new ReglaNegocioException(CodigoError.CitaNoModificable, "La cita ya no está agendada.");
        if (fechaCita > hoy)
            throw new ReglaNegocioException(CodigoError.CitaNoModificable, "No se registra la asistencia de una cita futura.");
    }

    /// <summary>RN-12: corrección Atendida ↔ No asistió hasta 7 días después de la cita.</summary>
    /// <exception cref="ReglaNegocioException"><see cref="CodigoError.CitaNoModificable"/>.</exception>
    public static void ExigirCorreccionPermitida(EstadoCita estado, DateOnly fechaCita, DateOnly hoy)
    {
        if (estado is not (EstadoCita.Atendida or EstadoCita.NoAsistio))
            throw new ReglaNegocioException(CodigoError.CitaNoModificable, "Solo se corrige una cita Atendida o No asistió.");
        if (hoy > fechaCita.AddDays(DiasCorreccionAsistencia))
            throw new ReglaNegocioException(CodigoError.CitaNoModificable, "Pasaron más de 7 días desde la cita.");
    }
}
