using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Aplicacion.Reservas;

/// <summary>Reserva de una cita en un cupo, en línea o en ventanilla (RF-03, RF-10).</summary>
public sealed record CrearCita(
    int CupoId,
    string Curp,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string? Celular,
    string? Correo,
    bool AceptaAvisoPrivacidad,
    Guid ClaveIdempotencia,
    OrigenCita Origen,
    int? UsuarioId);

public sealed class CrearCitaValidador : AbstractValidator<CrearCita>
{
    public CrearCitaValidador()
    {
        RuleFor(x => x.Curp).Must(Curp.EsValida).WithMessage("La CURP no es válida.");
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("Escribe el nombre.").MaximumLength(80);
        RuleFor(x => x.ApellidoPaterno).NotEmpty().WithMessage("Escribe el primer apellido.").MaximumLength(80);
        RuleFor(x => x.ApellidoMaterno).MaximumLength(80);
        RuleFor(x => x.Correo).NotEmpty().WithMessage("Escribe un correo.").When(x => x.Origen == OrigenCita.Web);
        RuleFor(x => x.Correo).EmailAddress().WithMessage("El correo no es válido.").MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Correo));
        RuleFor(x => x.Celular).NotEmpty().WithMessage("Escribe un celular.").When(x => x.Origen == OrigenCita.Web);
        RuleFor(x => x.Celular).Matches("^[0-9]{10}$").WithMessage("Escribe 10 dígitos.")
            .When(x => !string.IsNullOrWhiteSpace(x.Celular));
        RuleFor(x => x.AceptaAvisoPrivacidad).Equal(true).WithMessage("Acepta el aviso de privacidad para continuar.");
        RuleFor(x => x.ClaveIdempotencia).NotEmpty();
    }
}

/// <summary>Resumen que se muestra al confirmar (docs/06#post-apipubliccitas).</summary>
public sealed record ModuloCita(int ModuloId, string Nombre, string Direccion);

public sealed record TramiteCita(int TipoTramiteId, string Nombre);

public sealed record CitaReservada(
    string Folio, EstadoCita Estado, DateOnly Fecha, TimeOnly Hora, ModuloCita Modulo, TramiteCita Tramite);

public sealed record CitaCreada(int CitaId, DateTimeOffset TokenVenceEn, CitaReservada Resumen);

/// <summary>
/// Reserva atómica (docs/09#la-reserva-paso-a-paso): el cupo se ocupa con un UPDATE condicional dentro de la
/// transacción de la cita y la confirmación queda en la bandeja de salida.
/// </summary>
public sealed class CrearCitaManejador(
    ITiieDb db,
    RelojAgenda reloj,
    ITokensCita tokens,
    IValidator<CrearCita> validador,
    IOptions<OpcionesSitio> sitio,
    Metricas metricas,
    ILogger<CrearCitaManejador> logger)
{
    private const string IndiceSolicitanteActivo = "UX_Cita_SolicitanteActiva";
    private const string IndiceCurp = "UX_Solicitante_Curp";

    public async Task<CitaCreada> EjecutarAsync(CrearCita cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        await validador.ValidateAndThrowAsync(cmd, ct);                                    // 422 Validacion

        // Reintento con la misma clave: se devuelve la cita ya creada.
        if (await PreviaAsync(cmd.ClaveIdempotencia, ct) is { } previa)
            return previa;

        try
        {
            var creada = await ReservarAsync(cmd, ct);
            metricas.ReservaCreada(cmd.Origen.ToString());
            return creada;
        }
        catch (Exception ex) when (ex is ReglaNegocioException or DbUpdateException)
        {
            // Dos peticiones con la misma clave al mismo tiempo: ganó la otra y esta devuelve su cita.
            if (await PreviaAsync(cmd.ClaveIdempotencia, ct) is { } ganadora)
                return ganadora;

            var codigo = ex switch
            {
                ReglaNegocioException regla => regla.Codigo,
                DbUpdateException db when db.ViolaIndice(IndiceSolicitanteActivo) || db.ViolaIndice(IndiceCurp) => CodigoError.YaTieneCita,
                _ => (CodigoError?)null,
            };
            if (codigo is null)
                throw;
            metricas.ReservaRechazada(codigo.Value);
            if (ex is ReglaNegocioException)
                throw;
            throw new ReglaNegocioException(CodigoError.YaTieneCita);                       // RN-01
        }
    }

    private async Task<CitaCreada> ReservarAsync(CrearCita cmd, CancellationToken ct)
    {
        var cupo = await db.Cupos.AsNoTracking()
            .Where(c => c.CupoId == cmd.CupoId)
            .Select(c => new
            {
                c.Fecha,
                c.Hora,
                c.Cerrado,
                c.Oferta.DiasVentana,
                OfertaActiva = c.Oferta.Activo,
                ModuloActivo = c.Oferta.Modulo.Activo,
                TipoActivo = c.Oferta.TipoTramite.Activo,
            })
            .SingleOrDefaultAsync(ct) ?? throw new NoEncontradoException();

        ReglasAgenda.ValidarReserva(cupo.Fecha, cupo.Hora, cupo.DiasVentana, cupo.Cerrado,
            ofertaActiva: cupo.OfertaActiva && cupo.ModuloActivo && cupo.TipoActivo,
            origen: cmd.Origen, ahoraLocal: reloj.AhoraLocal());                            // RN-03, RN-07, RN-09, RN-18

        var datosCurp = Curp.Interpretar(cmd.Curp);                                          // RN-15

        // Aviso temprano de RN-01 sin ocupar el cupo; el índice único sigue siendo la garantía.
        if (await db.Citas.AnyAsync(c => c.Solicitante.Curp == datosCurp.Curp && c.Estado == EstadoCita.Agendada, ct))
            throw new ReglaNegocioException(CodigoError.YaTieneCita);

        var citaId = await db.EnTransaccionAsync(async ct2 =>
        {
            if (await OperacionesCupo.OcuparAsync(db, cmd.CupoId, ct2) == 0)
                throw await OperacionesCupo.MotivoRechazoAsync(db, cmd.CupoId, ct2);          // RN-02

            var ahora = reloj.AhoraUtc();
            var solicitante = await Solicitantes.ObtenerOActualizarAsync(db, datosCurp,
                new DatosSolicitante(cmd.Nombre, cmd.ApellidoPaterno, cmd.ApellidoMaterno, cmd.Celular, cmd.Correo), ct2);
            var folio = await Folios.SiguienteAsync(db, reloj.HoyLocal(), ct2);              // RN-10
            var token = tokens.Emitir(cupo.Fecha);                                            // RN-16

            var cita = Cita.Agendar(folio, cmd.CupoId, solicitante, cmd.Origen, token, ahora,
                usuarioId: cmd.UsuarioId,
                claveIdempotencia: cmd.ClaveIdempotencia,
                versionAvisoPrivacidad: sitio.Value.VersionAvisoPrivacidad);
            db.Citas.Add(cita);
            if (solicitante.Correo is not null)
                db.Notificaciones.Add(Notificacion.Nueva(cita, TipoNotificacion.Confirmacion, solicitante.Correo, ahora));

            await db.SaveChangesAsync(ct2);
            return cita.CitaId;
        }, ct);

        var creada = await ResumenAsync(citaId, ct);
        logger.LogInformation("Cita {Folio} creada en el cupo {CupoId} ({Origen})", creada.Resumen.Folio, cmd.CupoId, cmd.Origen);
        return creada;
    }

    private async Task<CitaCreada?> PreviaAsync(Guid clave, CancellationToken ct)
    {
        var citaId = await db.Citas.AsNoTracking()
            .Where(c => c.ClaveIdempotencia == clave)
            .Select(c => (int?)c.CitaId)
            .SingleOrDefaultAsync(ct);
        return citaId is null ? null : await ResumenAsync(citaId.Value, ct);
    }

    private async Task<CitaCreada> ResumenAsync(int citaId, CancellationToken ct)
    {
        var cita = await ConsultasCita.ExigirAsync(db, citaId, ct);
        return new CitaCreada(cita.CitaId, cita.TokenVenceEn, new CitaReservada(
            cita.Folio, cita.Estado, cita.Fecha, cita.Hora,
            new ModuloCita(cita.ModuloId, cita.Modulo, cita.Direccion),
            new TramiteCita(cita.TipoTramiteId, cita.Tramite)));
    }
}
