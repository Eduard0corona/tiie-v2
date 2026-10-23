namespace Tiie.Dominio.Bitacora;

/// <summary>
/// Registro de la bitácora. Solo se inserta: la aplicación no lo edita ni lo borra (RF-19).
/// </summary>
public sealed class Auditoria
{
    public long AuditoriaId { get; set; }
    public DateTimeOffset Fecha { get; set; }
    /// <summary>Vacío para el sistema o un intento anónimo.</summary>
    public int? UsuarioId { get; set; }
    /// <summary>Por ejemplo <c>Cita.Cancelar</c>, <c>Sesion.Iniciar</c>, <c>Plantilla.Guardar</c>.</summary>
    public required string Accion { get; set; }
    public required string Entidad { get; set; }
    public string? EntidadId { get; set; }
    /// <summary>JSON con valores anteriores y nuevos, con datos personales enmascarados.</summary>
    public string? Cambios { get; set; }
    public string? Ip { get; set; }
}
