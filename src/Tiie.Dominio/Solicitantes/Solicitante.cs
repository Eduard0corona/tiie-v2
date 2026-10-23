using Tiie.Dominio.Comun;

namespace Tiie.Dominio.Solicitantes;

/// <summary>Persona identificada por su CURP. No tiene cuenta.</summary>
public sealed class Solicitante : IConMarcasDeTiempo
{
    public int SolicitanteId { get; set; }
    /// <summary>Mayúsculas y sin espacios; vacía solo después de anonimizar.</summary>
    public string? Curp { get; set; }
    public required string Nombre { get; set; }
    public required string ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    /// <summary>Derivada de la CURP (RN-15).</summary>
    public DateOnly? FechaNacimiento { get; set; }
    /// <summary>H, M o X; derivado de la CURP (RN-15).</summary>
    public char? Sexo { get; set; }
    public string? Celular { get; set; }
    public string? Correo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ModificadoEn { get; set; }
    public DateTimeOffset? AnonimizadoEn { get; set; }
}
