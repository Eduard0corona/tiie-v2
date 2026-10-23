using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Aplicacion.Reservas;

/// <summary>Solicitud de recuperación del enlace de la cita (RF-06).</summary>
public sealed record RecuperarEnlace(string Curp, string Correo);

public sealed class RecuperarEnlaceValidador : AbstractValidator<RecuperarEnlace>
{
    public RecuperarEnlaceValidador()
    {
        RuleFor(x => x.Curp).Must(Curp.EsValida).WithMessage("La CURP no es válida.");
        RuleFor(x => x.Correo).NotEmpty().WithMessage("Escribe tu correo.").EmailAddress().WithMessage("El correo no es válido.");
    }
}

/// <summary>
/// Si existe una cita Agendada de esa CURP con ese correo, encola el enlace a ese correo. Quien pregunta no sabe
/// el resultado: la respuesta es la misma exista o no la cita. Ningún dato de la cita sale de aquí (RN-16).
/// </summary>
public sealed class RecuperarEnlaceManejador(ITiieDb db, RelojAgenda reloj, IValidator<RecuperarEnlace> validador)
{
    /// <summary>Máximo de enlaces que se envían por CURP en una hora (RF-06).</summary>
    public const int MaximoPorHora = 3;

    public async Task EjecutarAsync(RecuperarEnlace cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        await validador.ValidateAndThrowAsync(cmd, ct);

        var curp = Curp.Normalizar(cmd.Curp);
        var correo = cmd.Correo.Trim();
        var ahora = reloj.AhoraUtc();

        var cita = await db.Citas.AsNoTracking()
            .Where(c => c.Solicitante.Curp == curp && c.Estado == EstadoCita.Agendada)
            .Select(c => new { c.CitaId, c.Solicitante.Correo, c.SolicitanteId })
            .SingleOrDefaultAsync(ct);
        if (cita?.Correo is null || !string.Equals(cita.Correo, correo, StringComparison.OrdinalIgnoreCase))
            return;

        var haceUnaHora = ahora.AddHours(-1);
        var recientes = await db.Notificaciones.CountAsync(n =>
            n.Tipo == TipoNotificacion.EnlaceRecuperacion && n.Cita.SolicitanteId == cita.SolicitanteId && n.CreadaEn >= haceUnaHora, ct);
        if (recientes >= MaximoPorHora)
            return;

        db.Notificaciones.Add(Notificacion.Nueva(cita.CitaId, TipoNotificacion.EnlaceRecuperacion, cita.Correo, ahora));
        await db.SaveChangesAsync(ct);
    }
}
