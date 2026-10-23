namespace Tiie.Aplicacion.Puertos;

/// <summary>
/// Alcance por módulo del usuario actual. Se resuelve con los claims de la sesión, sin consultar la base.
/// </summary>
public interface IAutorizacionModulo
{
    /// <summary>Vacío (<see langword="null"/>) = todos los módulos (Administrador).</summary>
    IReadOnlySet<int>? ModulosPermitidos { get; }

    bool PuedeAcceder(int moduloId);

    /// <summary>
    /// Lanza <see cref="Dominio.Errores.NoEncontradoException"/>: un recurso de otro módulo responde 404,
    /// para no revelar que existe.
    /// </summary>
    void ExigirAcceso(int moduloId);
}
