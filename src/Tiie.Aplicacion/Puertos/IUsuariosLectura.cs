namespace Tiie.Aplicacion.Puertos;

/// <summary>Nombres del personal para mostrar quién hizo un cambio. Las cuentas viven en Identity.</summary>
public interface IUsuariosLectura
{
    /// <summary>Nombre de usuario por ID; los IDs que no existen no aparecen.</summary>
    Task<IReadOnlyDictionary<int, string>> NombresAsync(IEnumerable<int> usuarioIds, CancellationToken ct);
}
