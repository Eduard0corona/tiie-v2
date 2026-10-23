using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Solicitantes;
using Tiie.Infraestructura.Datos;
using Tiie.Infraestructura.Datos.Semilla;
using Tiie.Integracion.Tests.Infraestructura;

namespace Tiie.Integracion.Tests.Datos;

/// <summary>Esquema, restricciones, secuencia del folio, interceptores y semilla (docs/05).</summary>
public sealed class DatosTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica)
{
    /// <summary>Oferta con un cupo de capacidad 1 en el módulo de prueba.</summary>
    private async Task<(int OfertaId, int CupoId)> CupoAsync()
    {
        var moduloId = await ModuloAsync();
        return await ConDbAsync(async db =>
        {
            var tipo = new Tiie.Dominio.Catalogos.TipoTramite { Nombre = $"Trámite {Guid.NewGuid():N}" };
            var oferta = new Oferta { ModuloId = moduloId, TipoTramite = tipo, DiasVentana = 49 };
            var cupo = new Cupo { Oferta = oferta, Fecha = new DateOnly(2026, 11, 12), Hora = new TimeOnly(10, 30), Capacidad = 1 };
            db.Cupos.Add(cupo);
            await db.SaveChangesAsync();
            return (oferta.OfertaId, cupo.CupoId);
        });
    }

    private static Cita NuevaCita(int cupoId, Solicitante solicitante, EstadoCita estado) => new()
    {
        Folio = $"261103-{Random.Shared.Next(1_000_000, 9_999_999)}",
        CupoId = cupoId,
        Solicitante = solicitante,
        Estado = estado,
        Origen = OrigenCita.Web,
        TokenHash = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray(),
        TokenProtegido = "protegido",
        TokenVenceEn = DateTimeOffset.UtcNow.AddDays(10),
        CreadaEn = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task La_migracion_siembra_los_roles_y_los_niveles_escolares()
    {
        var (roles, niveles) = await ConDbAsync(async db =>
            (await db.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync(),
             await db.NivelesEscolares.OrderBy(n => n.Orden).Select(n => n.Nombre).ToListAsync()));

        roles.ShouldBe([Roles.Administrador, Roles.Capturista, Roles.Supervisor]);
        niveles.ShouldBe(["Básico (preescolar, primaria y secundaria)", "Medio superior y superior"]);
    }

    [Fact]
    public async Task RN10_la_secuencia_del_folio_empieza_en_un_millon_y_no_se_repite()
    {
        var valores = await ConDbAsync(async db =>
        {
            var lista = new List<long>();
            for (var i = 0; i < 3; i++)
            {
                // NEXT VALUE FOR no se permite en subconsultas: no se compone la consulta (nada de SingleAsync).
                var valor = await db.Database.SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.SecuenciaFolio AS Value").ToListAsync();
                lista.Add(valor.Single());
            }
            return lista;
        });

        valores.ShouldAllBe(v => v >= 1_000_000);
        valores.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task RN02_la_base_rechaza_mas_ocupados_que_la_capacidad()
    {
        var (_, cupoId) = await CupoAsync();

        var lleno = await ConDbAsync(db => db.Cupos.Where(c => c.CupoId == cupoId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, (short)1)));
        lleno.ShouldBe(1);

        await Should.ThrowAsync<Microsoft.Data.SqlClient.SqlException>(() => ConDbAsync(db => db.Cupos
            .Where(c => c.CupoId == cupoId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, c => c.Ocupados + 1))));
    }

    [Fact]
    public async Task RN02_el_update_condicional_no_afecta_filas_cuando_el_cupo_esta_lleno()
    {
        var (_, cupoId) = await CupoAsync();

        async Task<int> Ocupar() => await ConDbAsync(db => db.Cupos
            .Where(c => c.CupoId == cupoId && !c.Cerrado && c.Ocupados < c.Capacidad)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, c => c.Ocupados + 1)));

        (await Ocupar()).ShouldBe(1);
        (await Ocupar()).ShouldBe(0);
    }

    [Fact]
    public async Task RN01_la_base_rechaza_dos_citas_agendadas_del_mismo_solicitante()
    {
        var (_, cupoId) = await CupoAsync();
        var solicitante = new Solicitante { Curp = "GOMA080515MSLRRNA5", Nombre = "Ana", ApellidoPaterno = "Gómez" };
        await ConDbAsync(async db =>
        {
            db.Citas.Add(NuevaCita(cupoId, solicitante, EstadoCita.Cancelada));
            db.Citas.Add(NuevaCita(cupoId, solicitante, EstadoCita.Agendada));
            await db.SaveChangesAsync();
        });

        var error = await Should.ThrowAsync<DbUpdateException>(() => ConDbAsync(async db =>
        {
            var existente = await db.Solicitantes.SingleAsync();
            db.Citas.Add(NuevaCita(cupoId, existente, EstadoCita.Agendada));
            await db.SaveChangesAsync();
        }));
        error.InnerException!.Message.ShouldContain("UX_Cita_SolicitanteActiva");
    }

    [Fact]
    public async Task RN15_la_base_rechaza_un_sexo_distinto_de_H_M_o_X()
    {
        await Should.ThrowAsync<DbUpdateException>(() => ConDbAsync(async db =>
        {
            db.Solicitantes.Add(new Solicitante { Curp = "PERJ950101HSLRRN04", Nombre = "Juan", ApellidoPaterno = "Pérez", Sexo = 'Z' });
            await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Un_requisito_de_5000_caracteres_se_guarda_completo()
    {
        var texto = string.Concat(Enumerable.Repeat("Requisito largo con acentos: ñandú. ", 140))[..5000];

        var guardado = await ConDbAsync(async db =>
        {
            var tipo = new Tiie.Dominio.Catalogos.TipoTramite { Nombre = "Con requisito largo" };
            tipo.Requisitos.Add(new Tiie.Dominio.Catalogos.Requisito { Texto = texto, Orden = 1 });
            db.TiposTramite.Add(tipo);
            await db.SaveChangesAsync();
            return await db.Requisitos.AsNoTracking().Select(r => r.Texto).SingleAsync();
        });

        guardado.ShouldBe(texto);
    }

    [Fact]
    public async Task Las_marcas_de_tiempo_salen_del_reloj_inyectado()
    {
        var creacion = Fabrica.Tiempo.GetUtcNow();
        var moduloId = await ModuloAsync();
        Fabrica.Tiempo.Avanzar(TimeSpan.FromHours(3));
        await ConDbAsync(async db =>
        {
            var modulo = await db.Modulos.SingleAsync(m => m.ModuloId == moduloId);
            modulo.Telefono = "6670000000";
            await db.SaveChangesAsync();
        });

        var modulo = await ConDbAsync(db => db.Modulos.AsNoTracking().SingleAsync(m => m.ModuloId == moduloId));

        (modulo.CreadoEn - creacion).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
        (modulo.ModificadoEn!.Value - creacion - TimeSpan.FromHours(3)).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Los_cambios_del_personal_quedan_en_la_auditoria_con_datos_personales_enmascarados()
    {
        var admin = await UsuarioAsync(Roles.Administrador);
        var moduloId = await ModuloAsync();

        await ComoPersonalAsync(admin.Id, async servicios =>
        {
            var db = servicios.GetRequiredService<TiieDbContext>();
            var modulo = await db.Modulos.SingleAsync(m => m.ModuloId == moduloId);
            modulo.Nombre = "Módulo renombrado";
            db.Solicitantes.Add(new Solicitante
            {
                Curp = "GOMA080515MSLRRNA5",
                Nombre = "Ana",
                ApellidoPaterno = "Gómez",
                Correo = "ana@example.com",
                Celular = "6671234567",
            });
            await db.SaveChangesAsync();
        });

        var registros = await ConDbAsync(db => db.Auditorias.AsNoTracking().Where(a => a.UsuarioId == admin.Id).ToListAsync());
        var modulo = registros.Single(r => r.Accion == "Modulo.Modificar");
        modulo.EntidadId.ShouldBe(moduloId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var cambios = JsonDocument.Parse(modulo.Cambios!).RootElement.GetProperty("nombre");
        cambios.GetProperty("anterior").GetString().ShouldBe("Módulo de prueba");
        cambios.GetProperty("nuevo").GetString().ShouldBe("Módulo renombrado");

        var solicitante = registros.Single(r => r.Accion == "Solicitante.Crear");
        solicitante.EntidadId.ShouldNotBeNullOrEmpty();
        var datos = solicitante.Cambios!;
        datos.ShouldContain("GOMA************A5");
        datos.ShouldContain("a***@example.com");
        datos.ShouldContain("******4567");
        datos.ShouldNotContain("GOMA080515MSLRRNA5");
        datos.ShouldNotContain("ana@example.com");
        datos.ShouldNotContain("\"Ana\"");
    }

    [Fact]
    public async Task Sin_usuario_del_personal_los_cambios_no_se_auditan()
    {
        await ModuloAsync();

        (await ConDbAsync(db => db.Auditorias.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task El_sembrador_de_desarrollo_es_idempotente()
    {
        async Task Sembrar()
        {
            await using var alcance = Fabrica.Services.CreateAsyncScope();
            await alcance.ServiceProvider.GetRequiredService<ISembradorDesarrollo>().SembrarAsync(CancellationToken.None);
        }

        await Sembrar();
        var primera = await ConDbAsync(async db => (await db.Municipios.CountAsync(), await db.Ofertas.CountAsync(),
            await db.Plantillas.CountAsync(), await db.Users.CountAsync()));
        await Sembrar();
        var segunda = await ConDbAsync(async db => (await db.Municipios.CountAsync(), await db.Ofertas.CountAsync(),
            await db.Plantillas.CountAsync(), await db.Users.CountAsync()));

        primera.ShouldBe((20, 6, 6 * 70 + 5 * 7, 1));
        segunda.ShouldBe(primera);
    }
}
