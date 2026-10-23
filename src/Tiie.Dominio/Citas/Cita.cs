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
}
