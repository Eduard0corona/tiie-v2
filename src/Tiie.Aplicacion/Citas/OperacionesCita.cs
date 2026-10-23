using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;

namespace Tiie.Aplicacion.Citas;

/// <summary>
/// Cambios de <c>Cupo.Ocupados</c>: siempre un UPDATE condicional dentro de la transacción de la cita (RN-02,
/// ADR 0004). Nunca se lee <c>Ocupados</c>, se compara en C# y se escribe.
/// </summary>
public static class OperacionesCupo
{
    /// <summary>Ocupa un lugar si el cupo está abierto y tiene lugar.</summary>
    /// <returns>1 si lo ocupó; 0 si no había lugar o el cupo está cerrado.</returns>
    public static Task<int> OcuparAsync(ITiieDb db, int cupoId, CancellationToken ct) =>
        db.Cupos
            .Where(c => c.CupoId == cupoId && !c.Cerrado && c.Ocupados < c.Capacidad)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, c => (short)(c.Ocupados + 1)), ct);

    /// <summary>Libera un lugar del cupo.</summary>
    public static Task<int> LiberarAsync(ITiieDb db, int cupoId, CancellationToken ct) =>
        db.Cupos
            .Where(c => c.CupoId == cupoId && c.Ocupados > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, c => (short)(c.Ocupados - 1)), ct);

    /// <summary>
    /// 0 filas en <see cref="OcuparAsync"/>: el cupo estaba lleno o se cerró mientras tanto.
    /// </summary>
    public static async Task<ReglaNegocioException> MotivoRechazoAsync(ITiieDb db, int cupoId, CancellationToken ct)
    {
        var cerrado = await db.Cupos.AsNoTracking().Where(c => c.CupoId == cupoId).Select(c => c.Cerrado).SingleOrDefaultAsync(ct);
        return new ReglaNegocioException(cerrado ? CodigoError.CupoCerrado : CodigoError.SinCupo);
    }
}

/// <summary>Resultado de reprogramar: la cita nueva y su token, para el enlace o la cookie.</summary>
public sealed record CitaReprogramada(int CitaId, TokenAcceso Token);

/// <summary>Cancelar y reprogramar, compartidos por Mi cita y el panel del personal.</summary>
public sealed class OperacionesCita(ITiieDb db, RelojAgenda reloj, ITokensCita tokens)
{
    /// <summary>
    /// Cancela en una sola transacción: estado, lugar liberado y aviso. Quien llama ya validó el plazo (RN-13
    /// para el ciudadano, la hora de la cita para el personal).
    /// </summary>
    public Task CancelarAsync(DatosCita cita, string? motivo, int? usuarioId, TipoNotificacion aviso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cita);
        return db.EnTransaccionAsync(async ct2 =>
        {
            var ahora = reloj.AhoraUtc();
            var filas = await db.Citas
                .Where(c => c.CitaId == cita.CitaId && c.Estado == EstadoCita.Agendada)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadoCita.Cancelada)
                    .SetProperty(c => c.MotivoCambio, motivo)
                    .SetProperty(c => c.ActualizadaEn, ahora)
                    .SetProperty(c => c.ActualizadaPor, usuarioId), ct2);
            if (filas == 0)
                throw new ReglaNegocioException(CodigoError.CitaNoModificable);

            await OperacionesCupo.LiberarAsync(db, cita.CupoId, ct2);
            if (cita.Correo is not null)
            {
                db.Notificaciones.Add(Notificacion.Nueva(cita.CitaId, aviso, cita.Correo, ahora));
                await db.SaveChangesAsync(ct2);
            }
            return true;
        }, ct);
    }

    /// <summary>
    /// RN-17: en una sola transacción ocupa el cupo nuevo, marca la original como Reprogramada, libera el cupo
    /// anterior y crea la cita nueva con folio y token nuevos.
    /// </summary>
    /// <param name="reglas">Ventana que aplica: <see cref="OrigenCita.Web"/> para el ciudadano y
    /// <see cref="OrigenCita.Ventanilla"/> para el personal (RN-18).</param>
    public async Task<CitaReprogramada> ReprogramarAsync(
        DatosCita cita, int cupoNuevoId, string? motivo, int? usuarioId, OrigenCita reglas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cita);
        var nuevo = await db.Cupos.AsNoTracking()
            .Where(c => c.CupoId == cupoNuevoId)
            .Select(c => new
            {
                c.OfertaId,
                c.Fecha,
                c.Hora,
                c.Cerrado,
                c.Oferta.DiasVentana,
                Activa = c.Oferta.Activo && c.Oferta.Modulo.Activo && c.Oferta.TipoTramite.Activo,
            })
            .SingleOrDefaultAsync(ct)
            ?? throw Error("El horario elegido no existe.");
        if (nuevo.OfertaId != cita.OfertaId)
            throw Error("El horario nuevo debe ser del mismo trámite y la misma sede.");
        if (cupoNuevoId == cita.CupoId)
            throw Error("Elige un horario distinto al de tu cita.");
        ReglasAgenda.ValidarReserva(nuevo.Fecha, nuevo.Hora, nuevo.DiasVentana, nuevo.Cerrado, nuevo.Activa, reglas, reloj.AhoraLocal());

        return await db.EnTransaccionAsync(async ct2 =>
        {
            var ahora = reloj.AhoraUtc();
            if (await OperacionesCupo.OcuparAsync(db, cupoNuevoId, ct2) == 0)
                throw await OperacionesCupo.MotivoRechazoAsync(db, cupoNuevoId, ct2);

            var filas = await db.Citas
                .Where(c => c.CitaId == cita.CitaId && c.Estado == EstadoCita.Agendada)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadoCita.Reprogramada)
                    .SetProperty(c => c.MotivoCambio, motivo)
                    .SetProperty(c => c.ActualizadaEn, ahora)
                    .SetProperty(c => c.ActualizadaPor, usuarioId), ct2);
            if (filas == 0)
                throw new ReglaNegocioException(CodigoError.CitaNoModificable);
            await OperacionesCupo.LiberarAsync(db, cita.CupoId, ct2);

            var version = await db.Citas.Where(c => c.CitaId == cita.CitaId).Select(c => c.VersionAvisoPrivacidad).SingleAsync(ct2);
            var solicitante = await db.Solicitantes.SingleAsync(s => s.SolicitanteId == cita.SolicitanteId, ct2);
            var token = tokens.Emitir(nuevo.Fecha);
            var nueva = Cita.Agendar(
                await Folios.SiguienteAsync(db, reloj.HoyLocal(), ct2), cupoNuevoId, solicitante, cita.Origen, token, ahora,
                usuarioId: usuarioId, versionAvisoPrivacidad: version, citaOrigenId: cita.CitaId);
            db.Citas.Add(nueva);
            if (solicitante.Correo is not null)
                db.Notificaciones.Add(Notificacion.Nueva(nueva, TipoNotificacion.Reprogramacion, solicitante.Correo, ahora));
            await db.SaveChangesAsync(ct2);
            return new CitaReprogramada(nueva.CitaId, token);
        }, ct);
    }

    private static ValidationException Error(string mensaje) => new([new ValidationFailure("cupoId", mensaje)]);
}
