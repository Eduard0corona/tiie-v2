using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Catalogos;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Publico;
using Tiie.Aplicacion.Reloj;

namespace Tiie.Aplicacion;

public static class ExtensionesAplicacion
{
    /// <summary>Casos de uso, <see cref="RelojAgenda"/>, validadores y opciones de la agenda.</summary>
    public static IServiceCollection AgregarAplicacion(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OpcionesAgenda>()
            .Bind(configuration.GetSection(OpcionesAgenda.Seccion))
            .ValidateDataAnnotations()
            .Validate(o => ZonaHorariaExiste(o.ZonaHoraria), "Agenda:ZonaHoraria no es una zona horaria conocida.")
            .ValidateOnStart();
        services.AddOptions<OpcionesSitio>()
            .Bind(configuration.GetSection(OpcionesSitio.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OpcionesCaptcha>()
            .Bind(configuration.GetSection(OpcionesCaptcha.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RelojAgenda>();
        services.AddValidatorsFromAssembly(typeof(ExtensionesAplicacion).Assembly, includeInternalTypes: true);

        services.AddScoped<RegistroAuditoria>();
        services.AddScoped<ConsultarConfiguracionManejador>();
        services.AddScoped<ConsultarCatalogosAdminManejador>();
        services.AddScoped<ConsultarProcesosManejador>();

        return services;
    }

    private static bool ZonaHorariaExiste(string zona) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(zona, out _);
}
