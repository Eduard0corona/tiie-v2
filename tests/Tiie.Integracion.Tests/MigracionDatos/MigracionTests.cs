using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Integracion.Tests.Infraestructura;
using Tiie.Migracion;
using Tiie.Migracion.Destino;

namespace Tiie.Integracion.Tests.MigracionDatos;

/// <summary>
/// tools/Tiie.Migracion de punta a punta (docs/14): una base de 2016 de ejemplo con el esquema supuesto,
/// migrar, conciliar, repetir sin duplicar y encolar los avisos.
/// </summary>
public sealed class MigracionTests(FabricaTiie fabrica) : PruebaIntegracion(fabrica), IDisposable
{
    private const string BaseOrigen = "TIEDevPruebas";
    private const string Mapa = "0=Capturista,1=Supervisor,2=Administrador";

    private readonly DirectoryInfo _salida = Directory.CreateTempSubdirectory("tiie-migracion-");
    private readonly DateOnly _corte = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Mazatlan")).DateTime);

    private string Origen => new SqlConnectionStringBuilder(Fabrica.CadenaConexion) { InitialCatalog = BaseOrigen }.ConnectionString;

    private static string Curp(int n) => GeneradorCurp.Valida(9000 + n);

    public void Dispose() => _salida.Delete(recursive: true);

    private string Dia(int dias) => _corte.AddDays(dias).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task CrearOrigenAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(Fabrica.CadenaConexion) { InitialCatalog = "master" }.ConnectionString;
        await EjecutarAsync(master, $"""
            IF DB_ID('{BaseOrigen}') IS NOT NULL
            BEGIN
                ALTER DATABASE {BaseOrigen} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE {BaseOrigen};
            END;
            CREATE DATABASE {BaseOrigen};
            """);
        await EjecutarAsync(Origen, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Origen", "esquema-supuesto.sql")));
        await EjecutarAsync(Origen, $"""
            INSERT dbo.Entidad VALUES (25, 'Sinaloa', 1);
            INSERT dbo.Municipio VALUES (6, 25, 'Culiacán'), (12, 25, 'Mazatlán');
            INSERT dbo.Modulo VALUES
                (1, 25, 6, 'Culiacán Centro', 'Av. Obregón', '100', NULL, 'Centro', '80000', 'Culiacán', '6671234567', 'centro@example.com', 1),
                (2, 25, 12, 'Mazatlán', 'Av. del Mar', NULL, NULL, 'Centro', NULL, 'Mazatlán', NULL, NULL, 1),
                (3, 25, 99, 'Sin municipio', 'Calle', NULL, NULL, 'Colonia', '80000', NULL, NULL, NULL, 1);
            INSERT dbo.Tramite VALUES
                (10, 1, 'Expedición de TIIE', 'Primera vez', 150, NULL, 'Acta de nacimiento', 1),
                (11, 2, 'EXPEDICION DE TIIE', 'Primera vez', 150, NULL, 'Acta de nacimiento', 1),
                (13, 1, 'Expedicion de TIIE', 'Primera vez', 150, NULL, 'Acta de nacimiento', 1),
                (21, 2, 'Reposición de TIIE', 'Extravío', 100, NULL, 'Denuncia', 1),
                (30, 3, 'Expedición de TIIE', NULL, NULL, NULL, NULL, 1);
            INSERT dbo.Horario VALUES
                (100, 10, '09:00', 2, 1), (101, 10, '14:00', 1, 1), (102, 13, '09:00', 3, 1),
                (110, 11, '10:00', 2, 1), (210, 21, '10:00', 1, 1), (211, 21, '05:00', 1, 1), (300, 30, '09:00', 1, 1);
            INSERT dbo.Cita VALUES
                (1, 'A-1', '{Curp(1)}', 'Ana', 'Gómez', NULL, '6671112233', 'a1@example.com', '{Dia(2)}', 'VIGENTE', NULL),
                (2, 'A-2', '{Curp(2)}', 'Beto', 'Ruiz', NULL, NULL, 'a2@example.com', '{Dia(2)}', 'VIGENTE', NULL),
                (3, 'A-3', '{Curp(2)}', 'Beto', 'Ruiz', NULL, NULL, 'a3@example.com', '{Dia(3)}', 'VIGENTE', NULL),
                (4, 'A-4', '{Curp(4)}', 'Caro', 'Paz', NULL, NULL, 'a4@example.com', '{Dia(3)}', 'VIGENTE', NULL),
                (5, 'A-5', 'INVALIDA', 'Dora', 'Luna', NULL, NULL, 'a5@example.com', '{Dia(4)}', 'VIGENTE', NULL),
                (6, 'A-6', '{Curp(6)}', 'Eli', 'Mora', NULL, NULL, 'a6@example.com', '{Dia(4)}', 'VIGENTE', NULL),
                (7, 'A-7', '{Curp(7)}', 'Fer', 'Sol', NULL, NULL, 'a7@example.com', '{Dia(4)}', 'vigente ', NULL),
                (8, 'A-8', '{Curp(8)}', 'Gil', 'Rey', NULL, NULL, NULL, '{Dia(4)}', 'VIGENTE', NULL),
                (9, 'A-9', '{Curp(9)}', 'Hugo', 'Vega', NULL, NULL, 'a9@example.com', '{Dia(2)}', 'CANCELADA', NULL),
                (10, 'A-10', '{Curp(10)}', 'Iris', 'Cano', NULL, NULL, 'a10@example.com', '{Dia(2)}', 'VIGENTE', NULL),
                (11, 'A-11', '{Curp(11)}', 'Juan', 'Ríos', NULL, NULL, 'a11@example.com', '{Dia(2)}', 'VIGENTE', NULL),
                (20, 'P-20', '{Curp(20)}', 'Kim', 'Lara', NULL, NULL, NULL, '{Dia(-40)}', 'ATENDIDA', NULL),
                (21, 'P-21', '{Curp(21)}', 'Leo', 'Mar', NULL, NULL, NULL, '{Dia(-40)}', 'ATENDIDA', NULL),
                (22, 'P-22', '{Curp(22)}', 'Max', 'Nur', NULL, NULL, NULL, '{Dia(-10)}', 'CANCELADA', NULL),
                (23, 'P-23', '{Curp(23)}', 'Noa', 'Oro', NULL, NULL, NULL, '{Dia(-10)}', 'ATENDIDA', NULL);
            INSERT dbo.HorarioCita VALUES
                (1, 1, 100), (2, 2, 110), (3, 3, 110), (4, 4, 999), (5, 5, 101), (6, 6, 101), (7, 7, 101), (8, 8, 101),
                (9, 9, 100), (10, 10, 300), (11, 11, 102), (20, 20, 100), (21, 21, 102), (22, 22, 110);
            INSERT dbo.Usuario VALUES
                (1, 'admin.anterior', 'Ana', 'Gómez', NULL, 'admin.anterior@example.com', 2, NULL, 1),
                (2, 'captura1', 'Luis', 'Pérez', NULL, 'captura1@example.com', 0, 1, 1),
                (3, 'baja', 'Raúl', 'Soto', NULL, 'baja@example.com', 0, 1, 0),
                (4, 'sin.correo', 'Iris', 'Paz', NULL, NULL, 1, 1, 1);
            """);
    }

    private static async Task EjecutarAsync(string cadena, string sql)
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<(int Codigo, string Salida)> TiieMigracionAsync(params string[] args)
    {
        using var salida = new StringWriter();
        using var errores = new StringWriter();
        var codigo = await Consola.EjecutarAsync(args, salida, errores, CancellationToken.None);
        return (codigo, salida + errores.ToString());
    }

    private string Decisiones()
    {
        var ruta = Path.Combine(_salida.FullName, "decisiones-duplicados.csv");
        File.WriteAllLines(ruta, ["Curp,FolioConservar", $"{Curp(2)},A-3"]);
        return ruta;
    }

    private Task<(int, string)> MigrarAsync() => TiieMigracionAsync(
        "migrar", "--origen", Origen, "--destino", Fabrica.CadenaConexion, "--fecha-corte", Dia(0), "--mapa-roles", Mapa,
        "--decisiones", Decisiones(), "--llaves-locales", "--salida", _salida.FullName);

    private Task<(int, string)> ConciliarAsync() => TiieMigracionAsync(
        "conciliar", "--origen", Origen, "--destino", Fabrica.CadenaConexion, "--fecha-corte", Dia(0), "--mapa-roles", Mapa,
        "--decisiones", Decisiones(), "--salida", _salida.FullName);

    [Fact]
    public async Task Migra_catalogos_citas_futuras_historicos_y_usuarios_y_la_conciliacion_cuadra()
    {
        await CrearOrigenAsync();

        var (codigo, salida) = await MigrarAsync();

        codigo.ShouldBe(0, salida);
        var ofertas = await ConDbAsync(db => db.Ofertas.AsNoTracking().Include(o => o.Modulo).Include(o => o.TipoTramite).Include(o => o.Plantilla)
            .Where(o => o.IdAnterior != null).ToListAsync());
        ofertas.Select(o => o.IdAnterior).Order().ShouldBe([10, 11, 21]);
        ofertas.Select(o => o.TipoTramiteId).Distinct().Count().ShouldBe(2);
        ofertas.Single(o => o.IdAnterior == 11).Modulo.CodigoPostal.ShouldBe("00000");

        // El trámite 13 se fusionó con el 10: su horario de 3 turnos queda en la oferta del 10.
        var expedicion = ofertas.Single(o => o.IdAnterior == 10).Plantilla;
        expedicion.Single(p => p.DiaSemana == DayOfWeek.Monday && p.Hora == new TimeOnly(9, 0)).Turnos.ShouldBe((byte)3);
        expedicion.ShouldContain(p => p.DiaSemana == DayOfWeek.Saturday && p.Hora == new TimeOnly(9, 0));
        ofertas.Single(o => o.IdAnterior == 21).Plantilla.ShouldNotContain(p => p.DiaSemana == DayOfWeek.Saturday);

        var citas = await ConDbAsync(db => db.Citas.AsNoTracking().Include(c => c.Cupo).Include(c => c.Solicitante)
            .Where(c => c.FolioAnterior != null).ToListAsync());
        citas.Select(c => c.FolioAnterior).Order().ShouldBe(["A-1", "A-11", "A-3", "A-5", "A-6", "A-7", "A-8"]);
        citas.ShouldAllBe(c => c.Estado == EstadoCita.Agendada && c.Origen == OrigenCita.Migracion && c.AvisoAceptadoEn == null);
        citas.Single(c => c.FolioAnterior == "A-5").Solicitante.Curp.ShouldBeNull();
        var sobrecupo = citas.Single(c => c.FolioAnterior == "A-6").Cupo;
        (sobrecupo.Capacidad, sobrecupo.Ocupados).ShouldBe(((short)4, (short)4));

        // El token protegido se puede recuperar con las mismas llaves y corresponde a su hash.
        await using (var servicios = Servicios.Crear(Fabrica.CadenaConexion))
        {
            var tokens = servicios.GetRequiredService<ITokensCita>();
            var cita = citas.First();
            tokens.Hash(tokens.Recuperar(cita.TokenProtegido)!).ShouldBe(cita.TokenHash);
        }

        var historico = await ConDbAsync(db => db.HistoricoCitas.SumAsync(h => h.Total));
        historico.ShouldBe(3);
        var usuarios = await ConDbAsync(db => db.Users.AsNoTracking().Include(u => u.Modulos).OrderBy(u => u.UserName).ToListAsync());
        usuarios.Select(u => u.UserName).ShouldBe(["admin.anterior", "captura1"]);
        usuarios.ShouldAllBe(u => u.PasswordHash == null && !u.EmailConfirmed);
        usuarios.Single(u => u.UserName == "captura1").Modulos.ShouldHaveSingleItem();

        File.ReadAllText(Path.Combine(_salida.FullName, "duplicados-curp.csv")).ShouldContain("Se conserva");
        File.ReadAllText(Path.Combine(_salida.FullName, "excepciones.csv")).ShouldContain("A-4");
        File.ReadAllText(Path.Combine(_salida.FullName, "horas-sospechosas.csv")).ShouldContain("05:00");

        var (conciliado, detalle) = await ConciliarAsync();
        conciliado.ShouldBe(0, detalle);
        detalle.ShouldNotContain("DIFERENCIA");
    }

    [Fact]
    public async Task Repetir_la_migracion_no_duplica_y_los_avisos_se_encolan_una_sola_vez()
    {
        await CrearOrigenAsync();
        (await MigrarAsync()).Item1.ShouldBe(0);

        var (codigo, salida) = await MigrarAsync();

        codigo.ShouldBe(0, salida);
        salida.ShouldContain("0 migradas, 7 ya estaban");
        (await ConDbAsync(db => db.Citas.CountAsync())).ShouldBe(7);
        (await ConDbAsync(db => db.Cupos.Where(c => c.Ocupados > 0).SumAsync(c => (int)c.Ocupados))).ShouldBe(7);
        (await ConDbAsync(db => db.Modulos.CountAsync())).ShouldBe(2);
        (await ConciliarAsync()).Item1.ShouldBe(0);

        (await TiieMigracionAsync("avisar", "--destino", Fabrica.CadenaConexion)).Item2.ShouldContain("6 avisos");
        (await TiieMigracionAsync("avisar", "--destino", Fabrica.CadenaConexion)).Item2.ShouldContain("0 avisos");
        (await ConDbAsync(db => db.Notificaciones.CountAsync(n => n.Tipo == TipoNotificacion.AvisoMigracion))).ShouldBe(6);
    }

    [Fact]
    public async Task Sin_llaves_compartidas_ni_mapa_de_roles_completo_no_escribe_nada()
    {
        await CrearOrigenAsync();

        var (sinLlaves, mensaje) = await TiieMigracionAsync(
            "migrar", "--origen", Origen, "--destino", Fabrica.CadenaConexion, "--fecha-corte", Dia(0), "--mapa-roles", Mapa);
        var (sinRol, mensajeRol) = await TiieMigracionAsync(
            "migrar", "--origen", Origen, "--destino", Fabrica.CadenaConexion, "--fecha-corte", Dia(0), "--mapa-roles", "0=Capturista",
            "--llaves-locales", "--salida", _salida.FullName);

        sinLlaves.ShouldBe(2);
        mensaje.ShouldContain("DataProtection__BlobUri");
        sinRol.ShouldBe(2);
        mensajeRol.ShouldContain("no cubre los valores de Usuario.Tipo: 1, 2");
        (await ConDbAsync(db => db.Modulos.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Analizar_detecta_que_el_origen_no_tiene_el_esquema_supuesto()
    {
        await CrearOrigenAsync();
        await EjecutarAsync(Origen, "ALTER TABLE dbo.Modulo DROP COLUMN Poblacion;");

        var (codigo, salida) = await TiieMigracionAsync("analizar", "--origen", Origen, "--salida", _salida.FullName);

        codigo.ShouldBe(1);
        salida.ShouldContain("Modulo.Poblacion");
    }

    [Fact]
    public async Task Analizar_genera_los_reportes_sin_escribir_en_la_version_2()
    {
        await CrearOrigenAsync();

        var (codigo, salida) = await TiieMigracionAsync("analizar", "--origen", Origen, "--fecha-corte", Dia(0), "--salida", _salida.FullName);

        codigo.ShouldBe(0, salida);
        salida.ShouldContain("Valores de Usuario.Tipo en usuarios activos: 0 (1 usuarios), 1 (1 usuarios), 2 (1 usuarios)");
        File.Exists(Path.Combine(_salida.FullName, "duplicados-curp.csv")).ShouldBeTrue();
        (await ConDbAsync(db => db.Modulos.CountAsync())).ShouldBe(0);
    }
}
