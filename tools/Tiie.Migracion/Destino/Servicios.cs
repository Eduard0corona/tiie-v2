using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion;
using Tiie.Aplicacion.Puertos;
using Tiie.Infraestructura;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Identidad;
using Tiie.Infraestructura.Llaves;

namespace Tiie.Migracion.Destino;

/// <summary>
/// Los mismos servicios que el sitio para escribir en la versión 2: <c>TiieDbContext</c> con sus interceptores
/// (marcas de tiempo y auditoría), Identity, <c>RelojAgenda</c> y los tokens de cita.
/// </summary>
public static class Servicios
{
    public static ServiceProvider Crear(string cadenaDestino, IEnumerable<KeyValuePair<string, string?>>? adicional = null)
    {
        // Variables de entorno primero: la cadena de --destino siempre gana. DataProtection__BlobUri y
        // DataProtection__KeyUri llegan por entorno, igual que en el sitio.
        var configuracion = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection([new("ConnectionStrings:Tiie", cadenaDestino), .. adicional ?? []])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuracion);
        services.AddLogging();
        services.AgregarInfraestructura(configuracion);
        services.AgregarAplicacion(configuracion);
        services.AddScoped<IUsuarioActual, UsuarioMigracion>();
        services.AddIdentityCore<Usuario>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<TiieDbContext>();
        // Los tokens se protegen con las llaves del sitio: si no, los enlaces del aviso de migración no abrirían.
        services.AgregarLlavesCompartidas(configuracion);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    public static OpcionesDataProtection LlavesConfiguradas() =>
        new ConfigurationBuilder().AddEnvironmentVariables().Build()
            .GetSection(OpcionesDataProtection.Seccion).Get<OpcionesDataProtection>() ?? new();

    /// <summary>La auditoría registra los cambios de la migración como del sistema, sin usuario ni IP.</summary>
    private sealed class UsuarioMigracion : IUsuarioActual
    {
        public int? UsuarioId => null;

        public string? Ip => null;
    }
}
