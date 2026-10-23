namespace Tiie.Aplicacion.Seguridad;

/// <summary>Roles del personal. Se siembran por migración en todos los entornos.</summary>
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Supervisor = "Supervisor";
    public const string Capturista = "Capturista";

    public static IReadOnlyList<string> Todos { get; } = [Administrador, Supervisor, Capturista];
}
