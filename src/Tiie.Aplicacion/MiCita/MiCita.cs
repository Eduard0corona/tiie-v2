using System.Buffers.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Privacidad;

namespace Tiie.Aplicacion.MiCita;

public sealed record ModuloMiCita(
    string Nombre, string Direccion, string Municipio, string? HorarioAtencion, decimal? Latitud, decimal? Longitud);

public sealed record TramiteMiCita(
    string Nombre, decimal? Costo, string? Notas, string? NotasSede, IReadOnlyList<RequisitoTexto> Requisitos);

/// <summary>Datos del solicitante, con la CURP y el correo enmascarados.</summary>
public sealed record SolicitanteMiCita(string NombreCompleto, string Curp, string Correo);

/// <summary>Detalle de la cita para el ciudadano (docs/06#mi-cita).</summary>
public sealed record DetalleMiCita(
    string Folio,
    string? FolioAnterior,
    EstadoCita Estado,
    DateOnly Fecha,
    TimeOnly Hora,
    int OfertaId,
    ModuloMiCita Modulo,
    TramiteMiCita Tramite,
    SolicitanteMiCita Solicitante,
    bool PuedeModificar,
    DateOnly ModificableHasta);

/// <summary>Cita a la que da acceso un token vigente.</summary>
public sealed record AccesoMiCita(int CitaId, DateTimeOffset TokenVenceEn);

/// <summary>Cambia el token del enlace por el acceso a su cita, si sigue vigente (RN-16).</summary>
public sealed class AccederConTokenManejador(ITiieDb db, ITokensCita tokens, RelojAgenda reloj)
{
    /// <summary>Longitud del token: 32 bytes en Base64 URL sin relleno.</summary>
    public const int LongitudToken = 43;

    public async Task<AccesoMiCita?> EjecutarAsync(string token, CancellationToken ct)
    {
        if (token is not { Length: LongitudToken } || !Base64Url.IsValid(token))
            return null;

        var hash = tokens.Hash(token);
        var ahora = reloj.AhoraUtc();
        return await db.Citas.AsNoTracking()
            .Where(c => c.TokenHash == hash && c.TokenVenceEn >= ahora)
            .Select(c => new AccesoMiCita(c.CitaId, c.TokenVenceEn))
            .SingleOrDefaultAsync(ct);
    }
}

/// <summary>Consulta, cancelación, reprogramación y comprobante de la cita de la cookie (RF-05).</summary>
public sealed class MiCitaManejador(
    ITiieDb db,
    RelojAgenda reloj,
    OperacionesCita operaciones,
    IGeneradorPdf pdf,
    IOptions<OpcionesAgenda> agenda,
    ILogger<MiCitaManejador> logger)
{
    public async Task<DetalleMiCita> ConsultarAsync(int citaId, CancellationToken ct) =>
        Detalle(await ConsultasCita.ExigirAsync(db, citaId, ct));

    /// <summary>RN-13: hasta las 23:59 del día anterior a la cita.</summary>
    public async Task<DetalleMiCita> CancelarAsync(int citaId, string? motivo, CancellationToken ct)
    {
        ValidarMotivo(motivo);
        var cita = await ConsultasCita.ExigirAsync(db, citaId, ct);
        ReglasCita.ExigirModificablePorCiudadano(cita.Estado, cita.Fecha, reloj.HoyLocal(), agenda.Value.DiasAnticipacionCambios);

        await operaciones.CancelarAsync(cita, string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(), null,
            TipoNotificacion.Cancelacion, ct);
        logger.LogInformation("Cita {Folio} cancelada por el ciudadano", cita.Folio);
        return await ConsultarAsync(citaId, ct);
    }

    /// <summary>RN-13 y RN-17: otro cupo de la misma oferta, dentro de la ventana en línea.</summary>
    public async Task<(DetalleMiCita Detalle, AccesoMiCita Acceso)> ReprogramarAsync(int citaId, int cupoId, CancellationToken ct)
    {
        var cita = await ConsultasCita.ExigirAsync(db, citaId, ct);
        ReglasCita.ExigirModificablePorCiudadano(cita.Estado, cita.Fecha, reloj.HoyLocal(), agenda.Value.DiasAnticipacionCambios);

        var nueva = await operaciones.ReprogramarAsync(cita, cupoId, null, null, OrigenCita.Web, ct);
        var detalle = await ConsultarAsync(nueva.CitaId, ct);
        logger.LogInformation("Cita {Folio} reprogramada por el ciudadano como {FolioNuevo}", cita.Folio, detalle.Folio);
        return (detalle, new AccesoMiCita(nueva.CitaId, nueva.Token.VenceEn));
    }

    public async Task<(byte[] Contenido, string Folio)> ComprobanteAsync(int citaId, CancellationToken ct)
    {
        var cita = await ConsultasCita.ExigirAsync(db, citaId, ct);
        return (pdf.Comprobante(cita), cita.Folio);
    }

    private DetalleMiCita Detalle(DatosCita c)
    {
        var dias = agenda.Value.DiasAnticipacionCambios;
        return new DetalleMiCita(
            c.Folio, c.FolioAnterior, c.Estado, c.Fecha, c.Hora, c.OfertaId,
            new ModuloMiCita(c.Modulo, c.Direccion, c.Municipio, c.HorarioAtencion, c.Latitud, c.Longitud),
            new TramiteMiCita(c.Tramite, c.Costo, c.Notas, c.NotasSede, c.Requisitos),
            new SolicitanteMiCita(c.NombreCompleto, Enmascarar.Curp(c.Curp), Enmascarar.Correo(c.Correo)),
            ReglasCita.CiudadanoPuedeModificar(c.Estado, c.Fecha, reloj.HoyLocal(), dias),
            ReglasCita.ModificableHasta(c.Fecha, dias));
    }

    private static void ValidarMotivo(string? motivo)
    {
        if (motivo is { Length: > 300 })
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("motivo", "Escribe 300 caracteres o menos.")]);
    }
}
