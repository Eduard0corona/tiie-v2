using Tiie.Dominio.Agenda;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Dominio.Citas;

/// <summary>Reserva de un solicitante en un cupo. Oferta, módulo, fecha y hora salen del cupo (RN-09).</summary>
public sealed class Cita
{
    public int CitaId { get; set; }
    /// <summary>Formato AAMMDD-NNNNNNN (RN-10).</summary>
    public required string Folio { get; set; }
    public int CupoId { get; set; }
    public Cupo Cupo { get; set; } = null!;
    public int SolicitanteId { get; set; }
    public Solicitante Solicitante { get; set; } = null!;
    public EstadoCita Estado { get; set; } = EstadoCita.Agendada;
    public OrigenCita Origen { get; set; }
    /// <summary>SHA-256 del token de acceso (RN-16). El token en claro nunca se guarda.</summary>
    public byte[] TokenHash { get; set; } = [];
    /// <summary>Token cifrado con Data Protection, solo para armar el enlace de los correos.</summary>
    public required string TokenProtegido { get; set; }
    public DateTimeOffset TokenVenceEn { get; set; }
    public Guid? ClaveIdempotencia { get; set; }
    public string? VersionAvisoPrivacidad { get; set; }
    public DateTimeOffset? AvisoAceptadoEn { get; set; }
    /// <summary>La cita que se reprogramó para crear esta.</summary>
    public int? CitaOrigenId { get; set; }
    public Cita? CitaOrigen { get; set; }
    /// <summary>Motivo de cancelación o reprogramación.</summary>
    public string? MotivoCambio { get; set; }
    /// <summary>Folio del sistema de 2016.</summary>
    public string? FolioAnterior { get; set; }
    public DateTimeOffset CreadaEn { get; set; }
    /// <summary>Usuario del personal; vacío si la creó el ciudadano.</summary>
    public int? CreadaPor { get; set; }
    public DateTimeOffset? ActualizadaEn { get; set; }
    /// <summary>Usuario del personal; vacío si fue el ciudadano o el sistema.</summary>
    public int? ActualizadaPor { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Notificacion> Notificaciones { get; } = [];

    /// <summary>Cita nueva en estado Agendada. El token ya viene emitido: aquí solo se guardan su hash y su copia protegida.</summary>
    public static Cita Agendar(
        string folio,
        int cupoId,
        Solicitante solicitante,
        OrigenCita origen,
        TokenAcceso token,
        DateTimeOffset ahora,
        int? usuarioId = null,
        Guid? claveIdempotencia = null,
        string? versionAvisoPrivacidad = null,
        int? citaOrigenId = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new Cita
        {
            Folio = folio,
            CupoId = cupoId,
            Solicitante = solicitante,
            Estado = EstadoCita.Agendada,
            Origen = origen,
            TokenHash = token.Hash,
            TokenProtegido = token.Protegido,
            TokenVenceEn = token.VenceEn,
            ClaveIdempotencia = claveIdempotencia,
            VersionAvisoPrivacidad = versionAvisoPrivacidad,
            AvisoAceptadoEn = versionAvisoPrivacidad is null ? null : ahora,
            CitaOrigenId = citaOrigenId,
            CreadaEn = ahora,
            CreadaPor = usuarioId,
        };
    }
}

/// <summary>
/// Token de acceso a una cita (RN-16): el valor en claro solo existe al emitirlo, para el correo o la cookie.
/// </summary>
/// <param name="Valor">Token en Base64 URL; nunca se guarda ni se registra.</param>
/// <param name="Hash">SHA-256 del token, para buscar la cita.</param>
/// <param name="Protegido">Token cifrado con Data Protection, para volver a armar el enlace.</param>
/// <param name="VenceEn">23:59:59 locales del día siguiente a la cita, en UTC.</param>
public sealed record TokenAcceso(string Valor, byte[] Hash, string Protegido, DateTimeOffset VenceEn);
