using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Errores;

namespace Tiie.Web.Errores;

/// <summary>
/// Traduce excepciones a Problem Details con <c>codigo</c>. Un error inesperado responde 500 sin detalles;
/// solo se registra con su traza.
/// </summary>
public sealed class ManejadorExcepciones(IProblemDetailsService problemas, ILogger<ManejadorExcepciones> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión: no hay a quién responder.
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var problema = exception switch
        {
            ValidationException validacion => ErroresApi.ProblemaValidacion(ErroresPorCampo(validacion)),
            ReglaNegocioException regla => Problema(regla.Codigo, regla.Message == regla.Codigo.ToString() ? null : regla.Message),
            NoEncontradoException => Problema(CodigoError.NoEncontrado),
            CaptchaInvalidoException => Problema(CodigoError.CaptchaInvalido),
            DbUpdateConcurrencyException => Problema(CodigoError.Conflicto),
            BadHttpRequestException solicitud => Problema(CodigoError.SolicitudInvalida, DetalleSolicitud(solicitud), solicitud.StatusCode),
            _ => null,
        };

        if (problema is null)
        {
            logger.LogError(exception, "Error no controlado en {Metodo} {Ruta}", httpContext.Request.Method, httpContext.Request.Path);
            problema = Problema(CodigoError.ErrorInterno);
        }

        httpContext.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception,
        });
    }

    private static ProblemDetails Problema(CodigoError codigo, string? detalle = null, int? estado = null)
    {
        var problema = new ProblemDetails
        {
            Status = estado ?? CatalogoErrores.Estado(codigo),
            Title = CatalogoErrores.Titulo(codigo),
            Detail = detalle,
        };
        problema.Extensions[ErroresApi.ExtensionCodigo] = codigo.ToString();
        return problema;
    }

    /// <summary>El mensaje de ASP.NET Core está en inglés y nombra parámetros internos: se resume en español.</summary>
    private static string DetalleSolicitud(BadHttpRequestException solicitud) => solicitud switch
    {
        { InnerException: JsonException } => "El cuerpo de la solicitud no es un JSON válido.",
        { StatusCode: StatusCodes.Status413PayloadTooLarge } => "La solicitud es demasiado grande.",
        _ => "Falta un dato obligatorio o tiene un formato inválido.",
    };

    private static Dictionary<string, string[]> ErroresPorCampo(ValidationException validacion) =>
        validacion.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}
