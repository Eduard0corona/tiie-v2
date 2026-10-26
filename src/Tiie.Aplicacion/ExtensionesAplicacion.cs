using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Catalogos;
using Tiie.Aplicacion.Citas;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Horarios;
using Tiie.Aplicacion.MiCita;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Procesos;
using Tiie.Aplicacion.Publico;
using Tiie.Aplicacion.Reloj;
using Tiie.Aplicacion.Reportes;
using Tiie.Aplicacion.Reservas;
using Tiie.Aplicacion.Sitio;

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
        services.AddOptions<OpcionesProcesos>().Bind(configuration.GetSection(OpcionesProcesos.Seccion));
        services.AddOptions<OpcionesCaptcha>()
            .Bind(configuration.GetSection(OpcionesCaptcha.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RelojAgenda>();
        services.AddValidatorsFromAssembly(typeof(ExtensionesAplicacion).Assembly, includeInternalTypes: true);

        services.AddSingleton<Metricas>();
        services.AddScoped<RegistroAuditoria>();
        services.AddScoped<OperacionesCita>();
        services.AddScoped<GeneradorCupos>();

        // Sitio público y Mi cita
        services.AddScoped<ConsultarConfiguracionManejador>();
        services.AddScoped<ConsultarCatalogoPublicoManejador>();
        services.AddScoped<ConsultarDisponibilidadManejador>();
        services.AddScoped<ConsultarCuposManejador>();
        services.AddScoped<PortadaManejador>();
        services.AddScoped<CrearCitaManejador>();
        services.AddScoped<RecuperarEnlaceManejador>();
        services.AddScoped<AccederConTokenManejador>();
        services.AddScoped<MiCitaManejador>();

        // Panel del personal
        services.AddScoped<ConsultarCatalogosAdminManejador>();
        services.AddScoped<ConsultarProcesosManejador>();
        services.AddScoped<ConsultarAuditoriaManejador>();
        services.AddScoped<ModulosManejador>();
        services.AddScoped<TiposTramiteManejador>();
        services.AddScoped<OfertasManejador>();
        services.AddScoped<PlantillasManejador>();
        services.AddScoped<CuposManejador>();
        services.AddScoped<BloqueosManejador>();
        services.AddScoped<CitasPersonalManejador>();
        services.AddScoped<ReportesManejador>();
        services.AddScoped<TableroManejador>();

        // Procesos (el planificador se registra en Tiie.Web)
        services.AddScoped<EnvioNotificaciones>();
        services.AddScoped<EjecutorProcesos>();
        services.AddScoped<Retencion>();
        services.AddScoped<IProcesoDiario, GeneracionCupos>();
        services.AddScoped<IProcesoDiario, CierreDiario>();
        services.AddScoped<IProcesoDiario, Recordatorios>();
        services.AddScoped<IProcesoDiario>(sp => sp.GetRequiredService<Retencion>());

        return services;
    }

    private static bool ZonaHorariaExiste(string zona) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(zona, out _);
}
