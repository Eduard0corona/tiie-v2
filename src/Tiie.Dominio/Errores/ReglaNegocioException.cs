namespace Tiie.Dominio.Errores;

/// <summary>
/// Una regla de negocio impide la operación. El host la traduce a Problem Details con su <see cref="Codigo"/>.
/// </summary>
public sealed class ReglaNegocioException : Exception
{
    public ReglaNegocioException(CodigoError codigo)
        : base(codigo.ToString()) => Codigo = codigo;

    public ReglaNegocioException(CodigoError codigo, string mensaje)
        : base(mensaje) => Codigo = codigo;

    public ReglaNegocioException()
        : this(CodigoError.Validacion) { }

    public ReglaNegocioException(string message)
        : this(CodigoError.Validacion, message) { }

    public ReglaNegocioException(string message, Exception innerException)
        : base(message, innerException) => Codigo = CodigoError.Validacion;

    public CodigoError Codigo { get; }
}
