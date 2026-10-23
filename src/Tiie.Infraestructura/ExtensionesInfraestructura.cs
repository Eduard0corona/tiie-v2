using System.ComponentModel.DataAnnotations;
using Azure.Communication.Email;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Puertos;
using Tiie.Infraestructura.Correo;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Datos.Interceptores;
using Tiie.Infraestructura.Datos.Semilla;

namespace Tiie.Infraestructura;

/// <summary><c>ConnectionStrings:Tiie</c>. En Azure usa <c>Authentication=Active Directory Default</c>, sin contraseña.</summary>
public sealed class OpcionesBaseDatos
{
    [Required(ErrorMessage = "Falta ConnectionStrings:Tiie.")]
    public string? CadenaConexion { get; set; }
}

public static class ExtensionesInfraestructura
{
    /// <summary><c>TiieDbContext</c> con sus interceptores, correo y semilla de desarrollo.</summary>
    public static IServiceCollection AgregarInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Credencial de Azure compartida: identidad administrada en Azure, sesión de az o de Visual Studio en local.
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        services.AddOptions<OpcionesBaseDatos>()
            .Configure(o => o.CadenaConexion = configuration.GetConnectionString("Tiie"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<MarcasDeTiempoInterceptor>();
        services.AddScoped<AuditoriaInterceptor>();
        services.AddDbContext<TiieDbContext>((sp, o) =>
        {
            // Sin cadena (herramientas de diseño de EF) se configura sin conexión; al arrancar el sitio,
            // OpcionesBaseDatos exige la cadena. Se lee de IConfiguration para no disparar esa validación aquí.
            var cadena = sp.GetRequiredService<IConfiguration>().GetConnectionString("Tiie");
            if (string.IsNullOrWhiteSpace(cadena))
                o.UseSqlServer(ConfigurarSql);
            else
                o.UseSqlServer(cadena, ConfigurarSql);
            o.AddInterceptors(sp.GetRequiredService<MarcasDeTiempoInterceptor>(), sp.GetRequiredService<AuditoriaInterceptor>());
        });
        services.AddScoped<ITiieDb>(sp => sp.GetRequiredService<TiieDbContext>());

        services.AddOptions<OpcionesCorreo>()
            .Bind(configuration.GetSection(OpcionesCorreo.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(sp => new EmailClient(
            new Uri(sp.GetRequiredService<IOptions<OpcionesCorreo>>().Value.AcsEndpoint!),
            sp.GetRequiredService<TokenCredential>()));
        services.AddScoped<ICorreo>(sp =>
            sp.GetRequiredService<IOptions<OpcionesCorreo>>().Value.Proveedor == ProveedorCorreo.Acs
                ? ActivatorUtilities.CreateInstance<CorreoAcs>(sp)
                : ActivatorUtilities.CreateInstance<CorreoArchivo>(sp));
        services.AddSingleton<IPlantillasCorreo, PlantillasCorreo>();

        services.AddOptions<OpcionesSemilla>().Bind(configuration.GetSection(OpcionesSemilla.Seccion));
        services.AddScoped<ISembradorDesarrollo, SembradorDesarrollo>();

        return services;
    }

    private static void ConfigurarSql(Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder sql) =>
        sql.EnableRetryOnFailure().CommandTimeout(30);
}
