using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tiie.Integracion.Tests.Infraestructura;

/// <summary>Cliente de la SPA: manda <c>X-Tiie-Csrf: 1</c> en todo y conserva las cookies.</summary>
public sealed class ClienteTiie : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public ClienteTiie(HttpClient http)
    {
        Http = http;
        Http.DefaultRequestHeaders.Add("X-Tiie-Csrf", "1");
    }

    public HttpClient Http { get; }

    public Task<HttpResponseMessage> GetAsync(string ruta) => Http.GetAsync(new Uri(ruta, UriKind.Relative));

    public Task<HttpResponseMessage> PostAsync(string ruta, object? cuerpo = null) =>
        Http.PostAsJsonAsync(new Uri(ruta, UriKind.Relative), cuerpo ?? new { }, Json);

    public Task<HttpResponseMessage> EntrarAsync(string usuario, string contrasena) =>
        PostAsync("/api/admin/auth/entrar", new { usuario, contrasena });

    public void Dispose() => Http.Dispose();
}

public static class RespuestasHttp
{
    public static async Task<T> LeerAsync<T>(this HttpResponseMessage respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);
        var valor = await respuesta.Content.ReadFromJsonAsync<T>(ClienteTiie.Json);
        return valor ?? throw new InvalidOperationException("La respuesta no tiene cuerpo.");
    }

    /// <summary>Problem Details de la respuesta como JSON, para revisar <c>codigo</c> y <c>errors</c>.</summary>
    public static async Task<JsonElement> ProblemaAsync(this HttpResponseMessage respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<string?> CodigoAsync(this HttpResponseMessage respuesta) =>
        (await respuesta.ProblemaAsync()).GetProperty("codigo").GetString();
}
