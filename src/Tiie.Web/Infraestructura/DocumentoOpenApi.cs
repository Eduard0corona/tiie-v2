using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tiie.Dominio.Errores;

namespace Tiie.Web.Infraestructura;

/// <summary>
/// Documento OpenAPI: se genera al compilar en <c>openapi/Tiie.Web.json</c> y de él salen los tipos de la SPA,
/// incluidos los de los errores (<c>ProblemaApi</c> y <c>CodigoError</c>).
/// </summary>
public static class DocumentoOpenApi
{
    private const string EsquemaProblema = "ProblemaApi";
    private const string EsquemaCodigo = nameof(CodigoError);

    public static IServiceCollection AgregarDocumentoOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info.Title = "TIIE v2 · Agenda de citas";
                documento.Info.Description = "API del sitio público, de Mi cita y del panel del personal. Ver docs/06-api.md.";
                AgregarErrores(documento);
                return Task.CompletedTask;
            });
            o.AddSchemaTransformer((esquema, contexto, _) =>
            {
                var tipo = contexto.JsonTypeInfo.Type;
                // ConvertidorHora escribe las horas como HH:mm.
                if (tipo == typeof(TimeOnly))
                {
                    esquema.Type = JsonSchemaType.String;
                    esquema.Format = null;
                    esquema.Pattern = "^([01][0-9]|2[0-3]):[0-5][0-9]$";
                }
                // Las enumeraciones viajan como texto (JsonStringEnumConverter).
                else if (tipo.IsEnum)
                {
                    esquema.Type = JsonSchemaType.String;
                }
                return Task.CompletedTask;
            });
        });

    /// <summary>Toda operación puede responder Problem Details con <c>codigo</c> (docs/06-api.md#errores).</summary>
    private static void AgregarErrores(OpenApiDocument documento)
    {
        documento.Components ??= new OpenApiComponents();
        documento.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        documento.Components.Schemas[EsquemaCodigo] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [.. Enum.GetNames<CodigoError>().Select(n => (JsonNode)JsonValue.Create(n))],
        };
        documento.Components.Schemas[EsquemaProblema] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "Problem Details (RFC 9457) con la extensión codigo.",
            Required = new HashSet<string> { "title", "status", "codigo", "traceId" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["codigo"] = new OpenApiSchemaReference(EsquemaCodigo, documento),
                ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["errors"] = new OpenApiSchema
                {
                    Description = "Solo en Validacion: mensajes por campo.",
                    Type = JsonSchemaType.Object,
                    AdditionalProperties = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchema { Type = JsonSchemaType.String },
                    },
                },
                ["desbloqueoEn"] = new OpenApiSchema
                {
                    Description = "Solo en CuentaBloqueada: instante UTC en que se desbloquea.",
                    Type = JsonSchemaType.String | JsonSchemaType.Null,
                    Format = "date-time",
                },
                ["usuarioId"] = new OpenApiSchema
                {
                    Description = "Solo en CorreoNoDisponible al crear un usuario: la cuenta quedó creada con este id.",
                    Type = JsonSchemaType.Integer | JsonSchemaType.Null,
                    Format = "int32",
                },
            },
        };

        var respuesta = new OpenApiResponse
        {
            Description = "Error como Problem Details con codigo.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(EsquemaProblema, documento) },
            },
        };
        foreach (var operacion in documento.Paths.Values.SelectMany(r => r.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>()))
        {
            operacion.Responses ??= [];
            operacion.Responses["default"] = respuesta;
        }
    }
}
