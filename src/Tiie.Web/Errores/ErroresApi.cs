using Microsoft.AspNetCore.Http.HttpResults;
using Tiie.Dominio.Errores;

namespace Tiie.Web.Errores;

/// <summary>Respuestas de error como Problem Details con la extensión <c>codigo</c>.</summary>
public static class ErroresApi
{
    public const string ExtensionCodigo = "codigo";

    public static ProblemHttpResult Problema(
        CodigoError codigo, string? detalle = null, IEnumerable<KeyValuePair<string, object?>>? extensiones = null)
    {
        var todas = new Dictionary<string, object?> { [ExtensionCodigo] = codigo.ToString() };
        foreach (var (clave, valor) in extensiones ?? [])
            todas[clave] = valor;
        return TypedResults.Problem(
            detail: detalle,
            statusCode: CatalogoErrores.Estado(codigo),
            title: CatalogoErrores.Titulo(codigo),
            extensions: todas);
    }

    public static ProblemHttpResult SolicitudInvalida(string? detalle = null) => Problema(CodigoError.SolicitudInvalida, detalle);

    public static ProblemHttpResult Prohibido() => Problema(CodigoError.Prohibido);

    public static ProblemHttpResult NoEncontrado() => Problema(CodigoError.NoEncontrado);

    /// <summary>422 <c>Validacion</c> con los mensajes por campo en <c>errors</c>.</summary>
    public static ProblemHttpResult Validacion(IDictionary<string, string[]> errores) =>
        TypedResults.Problem(ProblemaValidacion(errores));

    public static HttpValidationProblemDetails ProblemaValidacion(IDictionary<string, string[]> errores)
    {
        var problema = new HttpValidationProblemDetails(errores)
        {
            Status = CatalogoErrores.Estado(CodigoError.Validacion),
            Title = CatalogoErrores.Titulo(CodigoError.Validacion),
        };
        problema.Extensions[ExtensionCodigo] = nameof(CodigoError.Validacion);
        return problema;
    }
}
