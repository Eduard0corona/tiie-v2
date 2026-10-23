using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Tiie.Aplicacion;
using Tiie.Aplicacion.Procesos;
using Tiie.Infraestructura;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Datos.Semilla;
using Tiie.Web.Endpoints;
using Tiie.Web.Errores;
using Tiie.Web.Infraestructura;
using Tiie.Web.Seguridad;

// La generación de OpenAPI al compilar arranca la aplicación sin servirla: no siembra ni corre procesos.
var generandoOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Al generar OpenAPI no hay configuración de Azure; en Development no se exige.
    EnvironmentName = generandoOpenApi ? Environments.Development : null,
});
if (generandoOpenApi)
{
    // La herramienta nunca abre la conexión; solo se necesita para pasar la validación al iniciar.
    builder.Configuration.AddInMemoryCollection(
        [new("ConnectionStrings:Tiie", builder.Configuration.GetConnectionString("Tiie") ?? "Server=sin-conexion")]);
}

builder.AgregarObservabilidad();                                                // OpenTelemetry → Azure Monitor
builder.Services.AgregarInfraestructura(builder.Configuration);                  // DbContext, correo, semilla
builder.Services.AgregarAplicacion(builder.Configuration);                       // casos de uso, RelojAgenda
builder.Services.AgregarSeguridad(builder.Configuration);                        // Identity, cookies, políticas
builder.Services.AgregarLimitesDeSolicitudes(builder.Configuration);
builder.Services.AgregarDocumentoOpenApi();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ProblemasApi.Completar);
builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddHealthChecks().AddDbContextCheck<TiieDbContext>();
if (!generandoOpenApi)
    builder.Services.AddHostedService<PlanificadorProcesos>();   // correos, cupos, recordatorios, cierre diario
builder.Services.ConfigureHttpJsonOptions(o =>
{
    // Números como números: así OpenAPI los declara integer y no integer | string.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.Converters.Add(new ConvertidorHora());
});
// Los errores de enlace de parámetros llegan al manejador de excepciones y salen como Problem Details.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IHostEnvironment>((o, entorno) =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (!entorno.IsDevelopment() && !entorno.IsEnvironment(Entornos.Pruebas))
    {
        // App Service agrega la IP del cliente al final de X-Forwarded-For: se confía solo en ese salto.
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        o.ForwardLimit = 1;
    }
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();
app.UseCabecerasDeSeguridad();      // CSP, nosniff, Referrer-Policy, Permissions-Policy
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();
app.MapEndpointsPublicos();         // /api/publico
app.MapEndpointsMiCita();           // /mi-cita/{token} y /api/mi-cita
app.MapEndpointsAdmin();            // /api/admin
app.MapHealthChecks("/salud");
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Una ruta /api que no existe responde 404 en JSON; nunca index.html.
app.Map("/api/{**resto}", () => ErroresApi.NoEncontrado()).ExcludeFromDescription();
app.MapFallbackToFile("index.html", Spa.SinCache);

if ((app.Environment.IsDevelopment() || app.Environment.IsEnvironment(Entornos.QA)) && !generandoOpenApi)
{
    await using var alcance = app.Services.CreateAsyncScope();
    await alcance.ServiceProvider.GetRequiredService<ISembradorDesarrollo>().SembrarAsync(app.Lifetime.ApplicationStopping);
}

await app.RunAsync();

/// <summary>Punto de entrada; parcial y público para <c>WebApplicationFactory</c> en las pruebas.</summary>
public partial class Program;
