using Azure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Seguridad;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Web.Seguridad;

/// <summary>Sección <c>DataProtection</c>: llaves compartidas entre instancias y ranuras.</summary>
public sealed class OpcionesDataProtection
{
    public const string Seccion = "DataProtection";

    /// <summary>Blob donde se guardan las llaves, por ejemplo <c>https://sttiieprod.blob.core.windows.net/dataprotection/llaves.xml</c>.</summary>
    public string? BlobUri { get; set; }

    /// <summary>Llave de Key Vault que cifra las llaves.</summary>
    public string? KeyUri { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(BlobUri) && !string.IsNullOrWhiteSpace(KeyUri);
}

public static class ExtensionesSeguridad
{
    /// <summary>Identity, cookies, políticas, alcance por módulo y Data Protection.</summary>
    public static IServiceCollection AgregarSeguridad(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddIdentityCore<Usuario>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedEmail = true;
                o.Password.RequiredLength = 12;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<TiieDbContext>()
            .AddSignInManager<GestorInicioSesion>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<FabricaClaimsUsuario>()
            .AddPasswordValidator<ValidadorContrasenasComunes>()
            .AddErrorDescriber<DescriptorErroresIdentidad>();

        services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(24));

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(EsquemasTiie.Cita, ConfigurarCookieCita)
            .AddIdentityCookies(c =>
            {
                c.ApplicationCookie!.Configure(ConfigurarCookieSesion);
                c.TwoFactorUserIdCookie!.Configure(o =>
                {
                    o.Cookie.Name = "tiie_2fa";
                    ConfigurarCookieComun(o);
                    o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                });
                c.TwoFactorRememberMeCookie!.Configure(o =>
                {
                    o.Cookie.Name = "tiie_equipo";
                    ConfigurarCookieComun(o);
                    o.ExpireTimeSpan = TimeSpan.FromDays(14);
                });
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.PersonalSinMfa, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser())
            .AddPolicy(Politicas.Personal, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimsTiie.Mfa, "true"))
            .AddPolicy(Politicas.Supervisor, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimsTiie.Mfa, "true")
                .RequireRole(Roles.Administrador, Roles.Supervisor))
            .AddPolicy(Politicas.Administrador, p => p
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimsTiie.Mfa, "true")
                .RequireRole(Roles.Administrador))
            .AddPolicy(Politicas.Cita, p => p
                .AddAuthenticationSchemes(EsquemasTiie.Cita)
                .RequireClaim(ClaimsTiie.CitaId));
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ManejadorResultadoAutorizacion>();

        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioActual, UsuarioActual>();
        services.AddScoped<IAutorizacionModulo, AutorizacionModulo>();

        AgregarDataProtection(services, configuration);
        return services;
    }

    private static void AgregarDataProtection(IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection(OpcionesDataProtection.Seccion);
        // Sin llaves compartidas, cada reinicio o instancia invalidaría sesiones y enlaces de los correos.
        // En desarrollo y en pruebas las llaves quedan en el perfil local.
        services.AddOptions<OpcionesDataProtection>()
            .Bind(seccion)
            .Validate<IHostEnvironment>(
                (o, env) => o.Configurado || env.IsDevelopment() || env.IsEnvironment(Entornos.Pruebas),
                "Faltan DataProtection:BlobUri y DataProtection:KeyUri.")
            .ValidateOnStart();

        var dataProtection = services.AddDataProtection().SetApplicationName("Tiie");
        var opciones = seccion.Get<OpcionesDataProtection>() ?? new();
        if (opciones.Configurado)
        {
            // En Azure las llaves viven en Storage, cifradas con una llave de Key Vault (docs/07#data-protection).
            // La identidad administrada del sitio da acceso a ambos.
            var credencial = new DefaultAzureCredential();
            dataProtection
                .PersistKeysToAzureBlobStorage(new Uri(opciones.BlobUri!), credencial)
                .ProtectKeysWithAzureKeyVault(new Uri(opciones.KeyUri!), credencial);
        }
    }

    private static void ConfigurarCookieComun(CookieAuthenticationOptions o)
    {
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
    }

    private static void ConfigurarCookieSesion(CookieAuthenticationOptions o)
    {
        o.Cookie.Name = "tiie_sesion";
        ConfigurarCookieComun(o);
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;

        // Una SPA no sigue redirecciones de login: responder con el código.
        o.Events.OnRedirectToLogin = c =>
        {
            c.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = c =>
        {
            c.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        o.Events.OnSigningIn = c =>
        {
            ValidadorSesion.MarcarInicio(c);
            return Task.CompletedTask;
        };
        // Identity ya valida el sello de seguridad aquí: se encadena, no se reemplaza.
        o.Events.OnValidatePrincipal = async c =>
        {
            await SecurityStampValidator.ValidatePrincipalAsync(c);
            if (c.Principal is not null)
                ValidadorSesion.ExigirDuracionMaxima(c, TimeSpan.FromHours(10));
        };
    }

    private static void ConfigurarCookieCita(CookieAuthenticationOptions o)
    {
        o.Cookie.Name = "tiie_cita";
        o.Cookie.Path = "/api/mi-cita";
        ConfigurarCookieComun(o);
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = c =>
        {
            c.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnValidatePrincipal = ValidadorSesion.ValidarCitaAsync;
    }
}
