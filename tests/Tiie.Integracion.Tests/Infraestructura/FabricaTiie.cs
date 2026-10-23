using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using Tiie.Aplicacion.Puertos;
using Tiie.Infraestructura.Datos;
using Tiie.Web.Seguridad;

[assembly: AssemblyFixture(typeof(Tiie.Integracion.Tests.Infraestructura.FabricaTiie))]
[assembly: Trait("Category", "Integracion")]

namespace Tiie.Integracion.Tests.Infraestructura;

/// <summary>
/// El sitio completo contra un SQL Server 2022 en contenedor, uno por ejecución. Las migraciones se
/// aplican una vez y Respawn limpia los datos entre pruebas. El reloj, el correo y los logs son dobles.
/// </summary>
public sealed class FabricaTiie : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string UrlPublica = "https://tiie.test";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private Respawner? _respawner;

    /// <summary>Empieza cada prueba en el instante real para que Identity y el cliente HTTP coincidan con el sitio.</summary>
    public RelojPruebas Tiempo { get; } = new();

    public CorreoEnMemoria Correos { get; } = new();

    public LogsEnMemoria Logs { get; } = new();

    public string CadenaConexion => new SqlConnectionStringBuilder(_sql.GetConnectionString())
    {
        InitialCatalog = "TiiePruebas",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();

        await using (var alcance = Services.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<TiieDbContext>().Database.MigrateAsync();

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        _respawner = await Respawner.CreateAsync(conexion, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
            // Datos sembrados por migración en todos los entornos.
            TablesToIgnore = [new Table("dbo", "__EFMigrationsHistory"), new Table("dbo", "Rol"), new Table("dbo", "NivelEscolar")],
            WithReseed = true,
        });
    }

    /// <summary>Base vacía, reloj en el instante real y sin correos ni logs de la prueba anterior.</summary>
    public async Task ReiniciarAsync()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await _respawner!.ResetAsync(conexion);
        Tiempo.Fijar(DateTimeOffset.UtcNow);
        Correos.Limpiar();
        Logs.Limpiar();
    }

    public ClienteTiie CrearCliente() => new(CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri(UrlPublica),
        AllowAutoRedirect = false,
        HandleCookies = true,
    }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Entornos.Pruebas);
        builder.UseSetting("ConnectionStrings:Tiie", CadenaConexion);
        builder.UseSetting("Sitio:UrlPublica", UrlPublica);
        builder.UseSetting("Sitio:VersionAvisoPrivacidad", "2026-01");
        builder.UseSetting("Correo:Proveedor", "Archivo");
        builder.UseSetting("Correo:Remitente", "no-responder@tiie.test");
        builder.UseSetting("Captcha:ClaveSitio", "clave-sitio-pruebas");
        builder.UseSetting("Captcha:ClaveSecreta", "clave-secreta-pruebas");
        builder.UseSetting("Semilla:ContrasenaAdministrador", "semilla-de-pruebas-larga");
        // Muchas pruebas entran desde la misma «IP»; el límite de entrar tiene su propia prueba.
        builder.UseSetting("Limites:Entrar:Permitidas", "10000");
        builder.UseSetting("Limites:Admin:Permitidas", "10000");

        // Todos los niveles, incluidos Debug de Identity y los comandos de EF, para revisar que no haya datos personales.
        builder.ConfigureLogging(l => l.AddProvider(Logs).AddFilter<LogsEnMemoria>(_ => true));
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Tiempo);
            s.RemoveAll<ICorreo>();
            s.AddSingleton<ICorreo>(Correos);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

/// <summary>Guarda los correos en lugar de enviarlos.</summary>
public sealed class CorreoEnMemoria : ICorreo
{
    private readonly ConcurrentQueue<MensajeCorreo> _enviados = new();

    public IReadOnlyList<MensajeCorreo> Enviados => [.. _enviados];

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken)
    {
        _enviados.Enqueue(mensaje);
        return Task.CompletedTask;
    }

    public void Limpiar() => _enviados.Clear();
}

/// <summary>Captura los mensajes de log ya formateados, para revisar que no tengan datos personales.</summary>
public sealed class LogsEnMemoria : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _mensajes = new();

    public IReadOnlyList<string> Mensajes => [.. _mensajes];

    public ILogger CreateLogger(string categoryName) => new Registrador(this);

    public void Limpiar() => _mensajes.Clear();

    public void Dispose() { }

    private sealed class Registrador(LogsEnMemoria logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            logs._mensajes.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception.Message));
    }
}
