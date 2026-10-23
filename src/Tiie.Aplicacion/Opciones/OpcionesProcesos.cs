namespace Tiie.Aplicacion.Opciones;

/// <summary>Sección <c>Procesos</c>. Las pruebas de integración los desactivan y los ejecutan directamente.</summary>
public sealed class OpcionesProcesos
{
    public const string Seccion = "Procesos";

    public bool Habilitados { get; set; } = true;
}
