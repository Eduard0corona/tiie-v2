using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Tiie.Web.Errores;

/// <summary>
/// Completa toda respuesta de Problem Details: agrega <c>traceId</c> y, si falta, el <c>codigo</c> y el
/// título en español según el código HTTP (por ejemplo, un 405 o un 404 de archivos estáticos).
/// </summary>
public static class ProblemasApi
{
    public static void Completar(ProblemDetailsContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var problema = contexto.ProblemDetails;
        problema.Extensions["traceId"] = Activity.Current?.Id ?? contexto.HttpContext.TraceIdentifier;

        if (!problema.Extensions.ContainsKey(ErroresApi.ExtensionCodigo))
        {
            var codigo = CatalogoErrores.PorEstado(problema.Status ?? contexto.HttpContext.Response.StatusCode);
            problema.Extensions[ErroresApi.ExtensionCodigo] = codigo.ToString();
            problema.Title = CatalogoErrores.Titulo(codigo);
        }
    }
}
