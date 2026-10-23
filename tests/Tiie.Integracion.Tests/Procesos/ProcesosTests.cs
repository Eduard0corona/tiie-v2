using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Procesos;
using Tiie.Dominio.Bitacora;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Procesos;

/// <summary>Bandeja de salida, cierre diario, recordatorios, retención y candados (docs/10).</summary>
public sealed partial class ProcesosTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    private static readonly DateOnly Jueves = new(2026, 11, 5);

    private async Task<int> ReservarAsync(DateOnly fecha, int n = 1)
    {
        var oferta = await OfertaAsync();
        var cupo = await CupoAsync(oferta.OfertaId, fecha, new TimeOnly(10, 30));
        using var cliente = Fabrica.CrearCliente();
        (await ReservarAsync(cliente, Solicitud(cupo, GeneradorCurp.Valida(n)))).StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ConDbAsync(db => db.Citas.OrderByDescending(c => c.CitaId).Select(c => c.CitaId).FirstAsync());
    }

    private Task<int> EnviarAsync() =>
        ConServiciosAsync(s => s.GetRequiredService<EnvioNotificaciones>().EjecutarAsync(CancellationToken.None));

    private Task<string> EjecutarAsync<T>() where T : IProcesoDiario =>
        ConServiciosAsync(s => s.GetServices<IProcesoDiario>().OfType<T>().Single().EjecutarAsync(CancellationToken.None));

    private Task<Notificacion> NotificacionAsync(TipoNotificacion tipo) =>
        ConDbAsync(db => db.Notificaciones.AsNoTracking().SingleAsync(n => n.Tipo == tipo));

    [Fact]
    public async Task RF04_la_confirmacion_sale_con_el_enlace_privado_y_el_comprobante()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        await ReservarAsync(Jueves);

        (await EnviarAsync()).ShouldBe(1);

        var correo = Fabrica.Correos.Enviados.Single();
        correo.Destinatario.ShouldBe("ana@example.com");
        correo.Asunto.ShouldBe("Tu cita para la TIIE: jueves 5 de noviembre de 2026 a las 10:30");
        correo.Adjuntos!.Single().TipoContenido.ShouldBe("application/pdf");
        correo.Html.ShouldContain("aviso-de-privacidad");
        correo.Texto.ShouldContain("CURP impresa.");
        (await NotificacionAsync(TipoNotificacion.Confirmacion)).Estado.ShouldBe(EstadoNotificacion.Enviada);

        // El enlace del correo abre Mi cita.
        var token = Enlace().Match(correo.Texto).Groups[1].Value;
        using var cliente = Fabrica.CrearCliente();
        (await cliente.GetAsync($"/mi-cita/{token}")).Headers.Location!.OriginalString.ShouldBe("/mi-cita");
    }

    [Fact]
    public async Task RF04_si_el_correo_falla_la_cita_existe_y_la_notificacion_se_reintenta_con_el_retraso_esperado()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        await ReservarAsync(Jueves);
        Fabrica.Correos.Fallar = true;

        await EnviarAsync();
        var primera = await NotificacionAsync(TipoNotificacion.Confirmacion);
        (await EnviarAsync()).ShouldBe(0);                                     // aún no toca reintentar
        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(1));
        await EnviarAsync();
        var segunda = await NotificacionAsync(TipoNotificacion.Confirmacion);

        (await ConDbAsync(db => db.Citas.SingleAsync())).Estado.ShouldBe(EstadoCita.Agendada);
        primera.Estado.ShouldBe(EstadoNotificacion.Fallida);
        primera.Intentos.ShouldBe((byte)1);
        primera.ProximoIntentoEn.ShouldBe(Local(Hoy, new TimeOnly(18, 1)));
        primera.UltimoError.ShouldBe("Proveedor no disponible (simulado).");
        segunda.Intentos.ShouldBe((byte)2);
        segunda.ProximoIntentoEn.ShouldBe(Local(Hoy, new TimeOnly(18, 6)));

        Fabrica.Correos.Fallar = false;
        Fabrica.Tiempo.Avanzar(TimeSpan.FromMinutes(5));
        await EnviarAsync();
        (await NotificacionAsync(TipoNotificacion.Confirmacion)).Estado.ShouldBe(EstadoNotificacion.Enviada);
    }

    [Fact]
    public async Task RF04_al_sexto_fallo_la_notificacion_se_descarta_y_se_registra_una_advertencia()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        await ReservarAsync(Jueves);
        Fabrica.Correos.Fallar = true;

        for (var i = 0; i < 6; i++)
        {
            await EnviarAsync();
            Fabrica.Tiempo.Avanzar(TimeSpan.FromHours(5));
        }

        (await NotificacionAsync(TipoNotificacion.Confirmacion)).Estado.ShouldBe(EstadoNotificacion.Descartada);
        Fabrica.Logs.Mensajes.ShouldContain(m => m.Contains("descartada tras 6 intentos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_confirmacion_de_una_cita_que_ya_no_esta_agendada_se_descarta_sin_enviarse()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        await ReservarAsync(Jueves);
        await ConDbAsync(db => db.Citas.ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.Cancelada)));

        await EnviarAsync();

        Fabrica.Correos.Enviados.ShouldBeEmpty();
        (await NotificacionAsync(TipoNotificacion.Confirmacion)).Estado.ShouldBe(EstadoNotificacion.Descartada);
    }

    [Fact]
    public async Task Dos_instancias_del_envio_no_mandan_el_mismo_correo()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var citaId = await ReservarAsync(Jueves);
        var ahora = Fabrica.Tiempo.GetUtcNow();
        await ConDbAsync(async db =>
        {
            db.Notificaciones.AddRange(Enumerable.Range(0, 59).Select(i =>
                Notificacion.Nueva(citaId, TipoNotificacion.EnlaceRecuperacion, $"persona{i}@example.com", ahora)));
            await db.SaveChangesAsync();
        });

        async Task Instancia()
        {
            while (await EnviarAsync() > 0) { }
        }
        await Task.WhenAll(Instancia(), Instancia());

        Fabrica.Correos.Enviados.Count.ShouldBe(60);
        Fabrica.Correos.Enviados.Select(c => c.Destinatario).Distinct().Count().ShouldBe(60);
        (await ConDbAsync(db => db.Notificaciones.CountAsync(n => n.Estado == EstadoNotificacion.Enviada))).ShouldBe(60);
    }

    [Fact]
    public async Task RN12_el_cierre_diario_solo_afecta_citas_agendadas_de_fechas_pasadas()
    {
        FijarReloj(Hoy.AddDays(-5), new TimeOnly(18, 0));
        var ayer = await ReservarAsync(Hoy.AddDays(-1), n: 1);
        var cancelada = await ReservarAsync(Hoy.AddDays(-1), n: 2);
        var hoy = await ReservarAsync(Hoy, n: 3);
        await ConDbAsync(db => db.Citas.Where(c => c.CitaId == cancelada).ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.Cancelada)));
        FijarReloj(Hoy, new TimeOnly(0, 30));

        (await EjecutarAsync<CierreDiario>()).ShouldBe("1 citas pasaron a No asistió.");

        var estados = await ConDbAsync(db => db.Citas.AsNoTracking().ToDictionaryAsync(c => c.CitaId, c => (c.Estado, c.ActualizadaPor)));
        estados[ayer].ShouldBe((EstadoCita.NoAsistio, (int?)null));
        estados[cancelada].Estado.ShouldBe(EstadoCita.Cancelada);
        estados[hoy].Estado.ShouldBe(EstadoCita.Agendada);
    }

    [Fact]
    public async Task RN14_los_recordatorios_son_para_las_citas_de_manana_creadas_antes_de_las_9_y_no_se_repiten()
    {
        FijarReloj(Hoy, new TimeOnly(8, 0));
        var antes = await ReservarAsync(Manana, n: 1);
        var sinCorreo = await ReservarAsync(Manana, n: 2);
        await ConDbAsync(db => db.Solicitantes.Where(s => db.Citas.Any(c => c.CitaId == sinCorreo && c.SolicitanteId == s.SolicitanteId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Correo, (string?)null)));  // sin correo no hay recordatorio
        FijarReloj(Hoy, new TimeOnly(9, 30));
        await ReservarAsync(Manana, n: 3);                                     // creada después de las 9:00
        await ReservarAsync(Manana.AddDays(1), n: 4);                          // no es de mañana

        await EjecutarAsync<Recordatorios>();
        await EjecutarAsync<Recordatorios>();

        var recordatorios = await ConDbAsync(db => db.Notificaciones.Where(n => n.Tipo == TipoNotificacion.Recordatorio).Select(n => n.CitaId).ToListAsync());
        recordatorios.ShouldBe([antes]);
    }

    [Fact]
    public async Task La_retencion_esta_desactivada_mientras_no_se_defina_el_plazo()
    {
        (await EjecutarAsync<Retencion>()).ShouldStartWith("Desactivada");
    }

    [Fact]
    public async Task Anonimizar_quita_curp_nombre_y_contacto_pero_conserva_la_cita()
    {
        FijarReloj(Hoy, new TimeOnly(18, 0));
        var citaId = await ReservarAsync(Jueves);
        var solicitanteId = await ConDbAsync(db => db.Citas.Where(c => c.CitaId == citaId).Select(c => c.SolicitanteId).SingleAsync());

        await ConServiciosAsync(s => s.GetRequiredService<Retencion>().AnonimizarAsync([solicitanteId], CancellationToken.None));

        var solicitante = await ConDbAsync(db => db.Solicitantes.AsNoTracking().SingleAsync());
        (solicitante.Curp, solicitante.Correo, solicitante.Celular).ShouldBe((null, null, null));
        solicitante.Nombre.ShouldBe(Retencion.TextoAnonimizado);
        solicitante.AnonimizadoEn.ShouldNotBeNull();
        solicitante.FechaNacimiento.ShouldNotBeNull();                         // estadística agregada
        (await ConDbAsync(db => db.Citas.CountAsync())).ShouldBe(1);
        (await NotificacionAsync(TipoNotificacion.Confirmacion)).Destinatario.ShouldBe(Retencion.TextoAnonimizado);
    }

    [Fact]
    public async Task Un_proceso_diario_registra_su_ejecucion_y_no_se_repite_el_mismo_dia()
    {
        FijarReloj(Hoy, new TimeOnly(0, 20));
        var oferta = await OfertaAsync(diasVentana: 7);
        var limite = Local(Hoy, new TimeOnly(0, 15));

        var primera = await ConServiciosAsync(s => s.GetRequiredService<EjecutorProcesos>()
            .EjecutarAsync(s.GetServices<IProcesoDiario>().OfType<GeneracionCupos>().Single(), limite, CancellationToken.None));
        var segunda = await ConServiciosAsync(s => s.GetRequiredService<EjecutorProcesos>()
            .EjecutarAsync(s.GetServices<IProcesoDiario>().OfType<GeneracionCupos>().Single(), limite, CancellationToken.None));

        primera.ShouldBeTrue();
        segunda.ShouldBeFalse();
        var ejecucion = await ConDbAsync(db => db.EjecucionesProceso.AsNoTracking().SingleAsync());
        ejecucion.Nombre.ShouldBe(nameof(GeneracionCupos));
        ejecucion.UltimoResultado.ShouldBe(EjecucionProceso.Correcto);
        ejecucion.Detalle!.ShouldStartWith("Creados 10,");
        (await ConDbAsync(db => db.Cupos.CountAsync(c => c.OfertaId == oferta.OfertaId))).ShouldBe(10);
    }

    [Fact]
    public async Task Con_dos_instancias_solo_una_obtiene_el_candado_del_proceso()
    {
        var proceso = new ProcesoLento();

        var resultados = await Task.WhenAll(
            ConServiciosAsync(s => s.GetRequiredService<EjecutorProcesos>().EjecutarAsync(proceso, null, CancellationToken.None)),
            ConServiciosAsync(s => s.GetRequiredService<EjecutorProcesos>().EjecutarAsync(proceso, null, CancellationToken.None)));

        resultados.Count(r => r).ShouldBe(1);
        proceso.Ejecuciones.ShouldBe(1);
    }

    [Fact]
    public async Task Un_proceso_que_falla_queda_registrado_como_Error()
    {
        await ConServiciosAsync(s => s.GetRequiredService<EjecutorProcesos>().EjecutarAsync(new ProcesoQueFalla(), null, CancellationToken.None));

        var ejecucion = await ConDbAsync(db => db.EjecucionesProceso.AsNoTracking().SingleAsync());
        ejecucion.UltimoResultado.ShouldBe(EjecucionProceso.Error);
        ejecucion.Detalle.ShouldBe("Falló con InvalidOperationException. Revisa los logs.");
    }

    private sealed class ProcesoLento : IProcesoDiario
    {
        private int _ejecuciones;

        public int Ejecuciones => _ejecuciones;
        public string Nombre => "PruebaLento";
        public TimeOnly Hora => TimeOnly.MinValue;
        public DayOfWeek? Dia => null;

        public async Task<string> EjecutarAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _ejecuciones);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return "Listo";
        }
    }

    private sealed class ProcesoQueFalla : IProcesoDiario
    {
        public string Nombre => "PruebaFalla";
        public TimeOnly Hora => TimeOnly.MinValue;
        public DayOfWeek? Dia => null;

        public Task<string> EjecutarAsync(CancellationToken ct) => throw new InvalidOperationException("dato sensible que no debe guardarse");
    }

    [GeneratedRegex(@"/mi-cita/([A-Za-z0-9_-]{43})")]
    private static partial Regex Enlace();
}
