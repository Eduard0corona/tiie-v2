namespace Tiie.Dominio.Bitacora;

/// <summary>Última ejecución de un proceso programado.</summary>
public sealed class EjecucionProceso
{
    public const string Correcto = "Correcto";
    public const string Error = "Error";

    public required string Nombre { get; set; }
    public DateTimeOffset? UltimoInicio { get; set; }
    public DateTimeOffset? UltimoFin { get; set; }
    public string? UltimoResultado { get; set; }
    /// <summary>Resumen, sin datos personales.</summary>
    public string? Detalle { get; set; }
}
