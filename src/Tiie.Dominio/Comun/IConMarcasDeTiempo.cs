namespace Tiie.Dominio.Comun;

/// <summary>
/// Entidad con fecha de creación. La llena el interceptor de EF Core con <see cref="TimeProvider"/>.
/// </summary>
public interface IConFechaCreacion
{
    DateTimeOffset CreadoEn { get; set; }
}

/// <summary>
/// Entidad con fecha de creación y de última modificación, ambas en UTC.
/// </summary>
public interface IConMarcasDeTiempo : IConFechaCreacion
{
    DateTimeOffset? ModificadoEn { get; set; }
}
