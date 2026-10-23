using System.Globalization;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Horarios;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Solicitantes;
using Tiie.Infraestructura.Datos;

namespace Tiie.Integracion.Tests.Infraestructura;

/// <summary>Oferta creada para una prueba.</summary>
public sealed record OfertaPrueba(int OfertaId, int ModuloId, int TipoTramiteId);

/// <summary>Ayudas para armar agendas: ofertas con plantilla, cupos y reservas, con el reloj fijado.</summary>
public abstract partial class PruebaIntegracion
{
    /// <summary>Lunes 2 de noviembre de 2026, 18:00 en Sinaloa (01:00 UTC del martes).</summary>
    protected static readonly DateOnly Hoy = new(2026, 11, 2);

    protected static readonly DateOnly Manana = Hoy.AddDays(1);

    private static readonly TimeSpan Sinaloa = TimeSpan.FromHours(-7);

    private static int _ofertas;

    /// <summary>Fija el reloj del sitio en una fecha y hora locales de Sinaloa.</summary>
    protected void FijarReloj(DateOnly fecha, TimeOnly hora) =>
        Fabrica.Tiempo.Fijar(new DateTimeOffset(fecha.ToDateTime(hora), Sinaloa));

    /// <summary>Instante UTC de una fecha y hora locales de Sinaloa.</summary>
    protected static DateTimeOffset Local(DateOnly fecha, TimeOnly hora) => new(fecha.ToDateTime(hora), Sinaloa);

    protected async Task<T> ConDbAsync<T>(Func<TiieDbContext, Task<T>> accion)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        return await accion(alcance.ServiceProvider.GetRequiredService<TiieDbContext>());
    }

    protected async Task ConDbAsync(Func<TiieDbContext, Task> accion)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        await accion(alcance.ServiceProvider.GetRequiredService<TiieDbContext>());
    }

    protected async Task<T> ConServiciosAsync<T>(Func<IServiceProvider, Task<T>> accion)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        return await accion(alcance.ServiceProvider);
    }

    /// <summary>
    /// Oferta con plantilla de lunes a viernes a las 9:00 y 10:00 con 2 turnos, salvo que se indique otra.
    /// </summary>
    protected async Task<OfertaPrueba> OfertaAsync(
        short diasVentana = 49,
        int? moduloId = null,
        IReadOnlyList<(DayOfWeek Dia, TimeOnly Hora, byte Turnos)>? plantilla = null,
        string? tramite = null)
    {
        var modulo = moduloId ?? await ModuloAsync();
        var numero = Interlocked.Increment(ref _ofertas);
        return await ConDbAsync(async db =>
        {
            var tipo = new TipoTramite { Nombre = tramite ?? $"Trámite {numero}", Costo = 0 };
            tipo.Requisitos.Add(new Requisito { Texto = "CURP impresa.", Orden = 1 });
            var oferta = new Oferta { ModuloId = modulo, TipoTramite = tipo, DiasVentana = diasVentana };
            foreach (var (dia, hora, turnos) in plantilla ?? Laborables([new TimeOnly(9, 0), new TimeOnly(10, 0)], 2))
                oferta.Plantilla.Add(new PlantillaHorario { DiaSemana = dia, Hora = hora, Turnos = turnos });
            db.Ofertas.Add(oferta);
            await db.SaveChangesAsync();
            return new OfertaPrueba(oferta.OfertaId, modulo, tipo.TipoTramiteId);
        });
    }

    protected static IReadOnlyList<(DayOfWeek, TimeOnly, byte)> Laborables(IReadOnlyList<TimeOnly> horas, byte turnos) =>
        [.. Enumerable.Range((int)DayOfWeek.Monday, 5).SelectMany(d => horas.Select(h => ((DayOfWeek)d, h, turnos)))];

    protected async Task<int> CupoAsync(int ofertaId, DateOnly fecha, TimeOnly hora, short capacidad = 1, bool cerrado = false) =>
        await ConDbAsync(async db =>
        {
            var cupo = new Cupo { OfertaId = ofertaId, Fecha = fecha, Hora = hora, Capacidad = capacidad, Cerrado = cerrado };
            db.Cupos.Add(cupo);
            await db.SaveChangesAsync();
            return cupo.CupoId;
        });

    protected Task<ResumenGeneracion> GenerarCuposAsync(int ofertaId) =>
        ConServiciosAsync(s => s.GetRequiredService<GeneradorCupos>().GenerarAsync(ofertaId, CancellationToken.None));

    protected Task<Cupo> LeerCupoAsync(int cupoId) =>
        ConDbAsync(db => db.Cupos.AsNoTracking().SingleAsync(c => c.CupoId == cupoId));

    /// <summary>Cuerpo de <c>POST /api/publico/citas</c> con datos ficticios.</summary>
    protected static object Solicitud(int cupoId, string curp, string? correo = "ana@example.com", string captcha = CaptchaPruebas.TokenValido) => new
    {
        cupoId,
        curp,
        nombre = "Ana",
        apellidoPaterno = "Gómez",
        apellidoMaterno = "Martínez",
        celular = "6671234567",
        correo,
        aceptaAvisoPrivacidad = true,
        tokenCaptcha = captcha,
    };

    protected static async Task<HttpResponseMessage> ReservarAsync(ClienteTiie cliente, object solicitud, Guid? clave = null)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/publico/citas", UriKind.Relative))
        {
            Content = JsonContent.Create(solicitud, options: ClienteTiie.Json),
        };
        mensaje.Headers.Add("Idempotency-Key", (clave ?? Guid.NewGuid()).ToString());
        return await cliente.Http.SendAsync(mensaje);
    }
}

/// <summary>CURPs ficticias con dígito verificador correcto (docs/12#infraestructura-de-pruebas-de-integración).</summary>
public static class GeneradorCurp
{
    /// <summary>CURP válida y distinta para cada <paramref name="n"/> (nacimientos desde el 1 de enero de 2000).</summary>
    public static string Valida(int n)
    {
        var nacimiento = new DateOnly(2000, 1, 1).AddDays(n % 9000);
        var homoclave = (char)('A' + (n / 9000 % 26));
        var base17 = string.Create(CultureInfo.InvariantCulture, $"PEGA{nacimiento:yyMMdd}MSLRRN{homoclave}");
        return base17 + Curp.CalcularDigitoVerificador(base17).ToString(CultureInfo.InvariantCulture);
    }
}
