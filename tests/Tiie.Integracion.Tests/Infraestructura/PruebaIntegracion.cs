using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Dominio.Catalogos;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Integracion.Tests.Infraestructura;

/// <summary>Usuario del personal creado para una prueba. <c>ClaveMfa</c> es la del autenticador, si la tiene.</summary>
public sealed record UsuarioPrueba(int Id, string Usuario, string Correo, string Contrasena, string? ClaveMfa);

/// <summary>Base de las pruebas de integración: cada prueba empieza con la base limpia.</summary>
public abstract class PruebaIntegracion(FabricaTiie fabrica) : IAsyncLifetime
{
    public const string Contrasena = "una-contrasena-de-pruebas";

    private static int _consecutivo;

    protected FabricaTiie Fabrica => fabrica;

    public async ValueTask InitializeAsync() => await fabrica.ReiniciarAsync();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected async Task<UsuarioPrueba> UsuarioAsync(
        string rol, bool conMfa = true, bool activo = true, bool correoConfirmado = true, params int[] modulos)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var nombre = $"{rol.ToLowerInvariant()}{Interlocked.Increment(ref _consecutivo)}";
        var usuario = new Usuario
        {
            UserName = nombre,
            Email = $"{nombre}@tiie.test",
            EmailConfirmed = correoConfirmado,
            Nombre = "Persona",
            ApellidoPaterno = "De Prueba",
            Activo = activo,
        };
        (await usuarios.CreateAsync(usuario, Contrasena)).Succeeded.ShouldBeTrue();
        (await usuarios.AddToRoleAsync(usuario, rol)).Succeeded.ShouldBeTrue();

        string? clave = null;
        if (conMfa)
        {
            await usuarios.ResetAuthenticatorKeyAsync(usuario);
            clave = await usuarios.GetAuthenticatorKeyAsync(usuario);
            await usuarios.SetTwoFactorEnabledAsync(usuario, true);
        }

        if (modulos.Length > 0)
        {
            var db = alcance.ServiceProvider.GetRequiredService<TiieDbContext>();
            db.UsuariosModulos.AddRange(modulos.Select(m => new UsuarioModulo { UsuarioId = usuario.Id, ModuloId = m }));
            await db.SaveChangesAsync();
        }
        return new UsuarioPrueba(usuario.Id, nombre, usuario.Email, Contrasena, clave);
    }

    /// <summary>Crea un módulo de ejemplo en Culiacán; siembra la entidad y el municipio si faltan.</summary>
    protected async Task<int> ModuloAsync(string nombre = "Módulo de prueba")
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<TiieDbContext>();
        if (!db.Entidades.Any())
        {
            var sinaloa = new Entidad { EntidadId = Entidad.Sinaloa, Nombre = "Sinaloa" };
            sinaloa.Municipios.Add(new Municipio { MunicipioId = 6, Nombre = "Culiacán" });
            db.Entidades.Add(sinaloa);
        }
        var modulo = new Modulo { MunicipioId = 6, Nombre = nombre, Calle = "Calle", Colonia = "Centro", CodigoPostal = "80000" };
        db.Modulos.Add(modulo);
        await db.SaveChangesAsync();
        return modulo.ModuloId;
    }

    /// <summary>Cliente con la sesión del usuario ya iniciada; si tiene MFA, también pasó el código.</summary>
    protected async Task<ClienteTiie> ClienteDeAsync(UsuarioPrueba usuario)
    {
        var cliente = fabrica.CrearCliente();
        var entrar = await cliente.EntrarAsync(usuario.Usuario, usuario.Contrasena);
        entrar.StatusCode.ShouldBe(HttpStatusCode.OK);
        if (usuario.ClaveMfa is not null)
        {
            var codigo = await cliente.PostAsync("/api/admin/auth/entrar/mfa",
                new { codigo = Totp.Generar(usuario.ClaveMfa, fabrica.Tiempo.GetUtcNow()), recordarEsteEquipo = false });
            codigo.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        return cliente;
    }

    protected async Task<ClienteTiie> ClienteComoAsync(string rol, params int[] modulos) =>
        await ClienteDeAsync(await UsuarioAsync(rol, modulos: modulos));

    /// <summary>Ejecuta código dentro de un alcance de servicios como si lo hiciera un usuario del personal.</summary>
    protected async Task ComoPersonalAsync(int usuarioId, Func<IServiceProvider, Task> accion)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var accesor = alcance.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accesor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString(CultureInfo.InvariantCulture))],
                IdentityConstants.ApplicationScheme)),
        };
        try
        {
            await accion(alcance.ServiceProvider);
        }
        finally
        {
            accesor.HttpContext = null;
        }
    }
}
