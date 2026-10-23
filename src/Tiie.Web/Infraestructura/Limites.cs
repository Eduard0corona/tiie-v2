using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Errores;
using Tiie.Web.Errores;

namespace Tiie.Web.Infraestructura;

/// <summary>Un límite: cuántas solicitudes se permiten en la ventana.</summary>
public sealed class Limite
{
    public int Permitidas { get; set; }
    public int VentanaMinutos { get; set; } = 1;
}

/// <summary>
/// Políticas de límites de solicitudes (docs/11#límites-de-solicitudes). Los valores se leen de la sección
/// <c>Limites</c> para ajustarlos sin desplegar. Los contadores son por instancia.
/// </summary>
public static class Limites
{
    public const string PublicoLectura = nameof(PublicoLectura);
    public const string Reserva = nameof(Reserva);
    public const string Recuperacion = nameof(Recuperacion);
    public const string EnlaceCita = nameof(EnlaceCita);
    public const string MiCita = nameof(MiCita);
    public const string Entrar = nameof(Entrar);
    public const string Admin = nameof(Admin);

    private static readonly Dictionary<string, Limite> Predeterminados = new()
    {
        [PublicoLectura] = new() { Permitidas = 120, VentanaMinutos = 1 },
        [Reserva] = new() { Permitidas = 10, VentanaMinutos = 10 },
        [Recuperacion] = new() { Permitidas = 5, VentanaMinutos = 60 },
        [EnlaceCita] = new() { Permitidas = 20, VentanaMinutos = 10 },
        [MiCita] = new() { Permitidas = 60, VentanaMinutos = 1 },
        [Entrar] = new() { Permitidas = 10, VentanaMinutos = 1 },
        [Admin] = new() { Permitidas = 300, VentanaMinutos = 1 },
    };

    public static IServiceCollection AgregarLimitesDeSolicitudes(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Dictionary<string, Limite>>(configuration.GetSection("Limites"));
        services.AddRateLimiter(o =>
        {
            o.OnRejected = async (contexto, ct) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                await ErroresApi.Problema(CodigoError.DemasiadasSolicitudes).ExecuteAsync(contexto.HttpContext);
            };

            Agregar(o, PublicoLectura, PorIp);
            Agregar(o, Reserva, PorIp);
            Agregar(o, Recuperacion, PorIp);
            Agregar(o, EnlaceCita, PorIp);
            Agregar(o, MiCita, h => h.User.FindFirstValue(ClaimsTiie.CitaId) ?? PorIp(h));
            Agregar(o, Entrar, PorIp);
            Agregar(o, Admin, h => h.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? $"u:{id}" : PorIp(h));
        });
        return services;
    }

    private static void Agregar(RateLimiterOptions o, string politica, Func<HttpContext, string> particion) =>
        o.AddPolicy(politica, http =>
        {
            var configurados = http.RequestServices.GetRequiredService<IOptionsMonitor<Dictionary<string, Limite>>>().CurrentValue;
            var limite = configurados.TryGetValue(politica, out var valor) ? valor : Predeterminados[politica];
            return RateLimitPartition.GetFixedWindowLimiter($"{politica}:{particion(http)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite.Permitidas,
                Window = TimeSpan.FromMinutes(limite.VentanaMinutos),
                QueueLimit = 0,
            });
        });

    private static string PorIp(HttpContext http) => $"ip:{http.Connection.RemoteIpAddress}";
}
