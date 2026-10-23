namespace Tiie.Dominio.Errores;

/// <summary>
/// El recurso no existe o no es visible para quien pregunta. Un recurso de otro módulo también
/// responde así, para no revelar que existe.
/// </summary>
public sealed class NoEncontradoException : Exception
{
    public NoEncontradoException()
        : base("El recurso no existe.") { }

    public NoEncontradoException(string message)
        : base(message) { }

    public NoEncontradoException(string message, Exception innerException)
        : base(message, innerException) { }
}
