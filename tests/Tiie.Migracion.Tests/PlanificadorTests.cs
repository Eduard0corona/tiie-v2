using Tiie.Migracion.Origen;
using Tiie.Migracion.Plan;

namespace Tiie.Migracion.Tests;

/// <summary>Reglas de docs/14#mapeo aplicadas sin base de datos.</summary>
public sealed class PlanificadorTests
{
    private static readonly DateOnly Corte = new(2026, 11, 30);
    private static readonly MapaRoles Roles = MapaRoles.Parsear("0=Capturista,1=Supervisor,2=Administrador");
    private const string CurpA = "GOMA080515MSLRRNA5";

    private static readonly List<EntidadOrigen> Entidades = [new(25, "Sinaloa", true)];
    private static readonly List<MunicipioOrigen> Municipios = [new(6, 25, "Culiacán")];

    private static ModuloOrigen Modulo(int id, int municipio = 6, string? cp = "80000", bool activo = true) =>
        new(id, municipio, $"Módulo {id}", "Av. Obregón", null, null, "Centro", cp, "Culiacán", "6671234567", null, activo);

    private static TramiteOrigen Tramite(int id, int? modulo, string nombre = "Expedición de TIIE", string? notas = null, string? requisitos = "Acta") =>
        new(id, modulo, nombre, "Detalle", 150m, notas, requisitos, true);

    private static CitaOrigen Cita(int id, string? curp, int? horario = 100, int? tramite = 10, int dias = 3, TimeOnly? hora = null) =>
        new(id, $"F-{id}", curp, "Ana", "Gómez", null, "667 123 4567", "ana@example.com", Corte.AddDays(dias), "VIGENTE", null,
            horario, tramite, horario is null ? null : hora ?? new TimeOnly(9, 0));

    private static DatosOrigen Datos(
        IEnumerable<ModuloOrigen>? modulos = null,
        IEnumerable<TramiteOrigen>? tramites = null,
        IEnumerable<HorarioOrigen>? horarios = null,
        IEnumerable<CitaOrigen>? citas = null,
        IEnumerable<TotalHistorico>? historicos = null,
        IEnumerable<UsuarioOrigen>? usuarios = null) =>
        new(Entidades, Municipios,
            [.. modulos ?? [Modulo(1)]],
            [.. tramites ?? [Tramite(10, 1)]],
            [.. horarios ?? [new HorarioOrigen(100, 10, new TimeOnly(9, 0), 2, true)]],
            [.. citas ?? []],
            [.. historicos ?? []],
            [.. usuarios ?? []]);

    private static PlanMigracion Planear(DatosOrigen datos, IReadOnlyDictionary<string, string>? decisiones = null) =>
        Planificador.Crear(datos, new OpcionesPlan(Corte, Roles, decisiones ?? Decisiones.Vacias));

    private static IReadOnlyList<string?[]> Filas(PlanMigracion plan, string archivo) =>
        plan.Reportes.Single(r => r.Archivo == archivo).Filas;

    [Theory]
    [InlineData(10, "09:00", true)]
    [InlineData(10, "12:30", true)]
    [InlineData(10, "12:31", false)]
    [InlineData(10, "08:59", false)]
    [InlineData(21, "10:00", false)]
    [InlineData(22, "10:00", false)]
    [InlineData(23, "10:00", false)]
    public void La_plantilla_abre_de_lunes_a_viernes_y_el_sabado_solo_en_la_manana_y_fuera_de_21_22_y_23(int tramite, string hora, bool sabado)
    {
        var dias = Planificador.DiasDePlantilla(tramite, TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture)).ToList();

        dias.ShouldBe(sabado
            ? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]
            : [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        dias.ShouldNotContain(DayOfWeek.Sunday);
    }

    [Theory]
    [InlineData("06:59", true)]
    [InlineData("07:00", false)]
    [InlineData("19:00", false)]
    [InlineData("19:01", true)]
    public void Las_horas_antes_de_las_7_o_despues_de_las_19_son_sospechosas(string hora, bool sospechosa) =>
        Planificador.EsHoraSospechosa(TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(sospechosa);

    [Fact]
    public void Los_tramites_se_agrupan_por_nombre_normalizado_con_los_valores_mas_comunes()
    {
        var plan = Planear(Datos(
            modulos: [Modulo(1), Modulo(2), Modulo(3)],
            tramites:
            [
                Tramite(10, 1, "Expedición de TIIE", requisitos: "Acta"),
                Tramite(11, 2, "EXPEDICION  DE tiie", requisitos: "Acta"),
                Tramite(12, 3, "Expedición de TIIE", requisitos: "Acta y CURP"),
            ]));

        var tipo = plan.Tipos.ShouldHaveSingleItem();
        tipo.Nombre.ShouldBe("Expedición de TIIE");
        tipo.Requisito.ShouldBe("Acta");
        plan.Ofertas.Count.ShouldBe(3);
        // La sede con requisitos distintos los conserva en sus notas y en el reporte.
        plan.Ofertas.Single(o => o.IdAnterior == 12).NotasSede.ShouldBe("Requisitos de la sede: Acta y CURP");
        Filas(plan, "diferencias-tramite.csv").ShouldHaveSingleItem()[0].ShouldBe("12");
    }

    [Fact]
    public void Dos_tramites_del_mismo_tipo_en_un_modulo_se_fusionan_en_una_oferta_con_el_mayor_numero_de_turnos()
    {
        var plan = Planear(Datos(
            tramites: [Tramite(10, 1), Tramite(13, 1, "Expedicion de TIIE")],
            horarios: [new(100, 10, new TimeOnly(9, 0), 2, true), new(101, 13, new TimeOnly(9, 0), 3, true)]));

        var oferta = plan.Ofertas.ShouldHaveSingleItem();
        oferta.IdAnterior.ShouldBe(10);
        oferta.Tramites.ShouldBe([10, 13]);
        plan.Plantillas.Where(p => p.Dia == DayOfWeek.Monday).ShouldHaveSingleItem().Turnos.ShouldBe((byte)3);
        Filas(plan, "ofertas-fusionadas.csv").ShouldHaveSingleItem();
    }

    [Fact]
    public void Un_tramite_sin_modulo_valido_no_genera_oferta_y_sus_citas_son_excepciones()
    {
        var plan = Planear(Datos(
            tramites: [Tramite(10, 1), Tramite(30, 99)],
            horarios: [new(100, 10, new TimeOnly(9, 0), 2, true), new(300, 30, new TimeOnly(9, 0), 2, true)],
            citas: [Cita(1, CurpA, horario: 300, tramite: 30)]));

        plan.Ofertas.ShouldHaveSingleItem().IdAnterior.ShouldBe(10);
        Filas(plan, "tramites-sin-modulo.csv").ShouldHaveSingleItem()[0].ShouldBe("30");
        plan.CitasAMigrar.ShouldBe(0);
        Filas(plan, "excepciones.csv").ShouldHaveSingleItem()[0].ShouldBe("F-1");
    }

    [Fact]
    public void Las_citas_sin_horario_van_a_excepciones_y_las_anteriores_al_corte_no_se_migran()
    {
        var plan = Planear(Datos(citas: [Cita(1, CurpA, horario: null), Cita(2, "PEGA000101MSLRRNA4", dias: -1)]));

        plan.CitasFuturasOrigen.ShouldBe(1);
        plan.CitasAMigrar.ShouldBe(0);
        Filas(plan, "excepciones.csv").ShouldHaveSingleItem()[2]!.ShouldContain("no tiene horario");
    }

    [Fact]
    public void RN01_una_curp_con_varias_citas_futuras_no_se_migra_sin_decision()
    {
        var plan = Planear(Datos(citas: [Cita(1, CurpA), Cita(2, CurpA.ToLowerInvariant(), dias: 5)]));

        plan.CitasAMigrar.ShouldBe(0);
        Filas(plan, "duplicados-curp.csv").Select(f => f[5]).ShouldAllBe(r => r == "Sin decisión: no se migra");
    }

    [Fact]
    public void RN01_con_decision_se_conserva_solo_el_folio_elegido()
    {
        var plan = Planear(Datos(citas: [Cita(1, CurpA), Cita(2, CurpA, dias: 5)]), new Dictionary<string, string> { [CurpA] = "F-2" });

        plan.Cupos.SelectMany(c => c.Citas).ShouldHaveSingleItem().Cita.Folio.ShouldBe("F-2");
        Filas(plan, "duplicados-curp.csv").Select(f => f[5]).ShouldBe(["Se descarta por decisión", "Se conserva"]);
    }

    [Fact]
    public void RN01_una_decision_que_no_corresponde_a_ninguna_cita_no_migra_ninguna()
    {
        var plan = Planear(Datos(citas: [Cita(1, CurpA), Cita(2, CurpA, dias: 5)]), new Dictionary<string, string> { [CurpA] = "F-9" });

        plan.CitasAMigrar.ShouldBe(0);
    }

    [Fact]
    public void Una_curp_invalida_se_migra_y_se_reporta()
    {
        var plan = Planear(Datos(citas: [Cita(1, "ABC"), Cita(2, "GOMA080515MSLRRNA9")]));

        var citas = plan.Cupos.SelectMany(c => c.Citas).ToList();
        citas.Count.ShouldBe(2);
        citas.Single(c => c.Cita.CitaId == 1).Curp.ShouldBeNull();
        // Mide 18: se guarda tal cual aunque el dígito verificador no cuadre.
        citas.Single(c => c.Cita.CitaId == 2).ShouldSatisfyAllConditions(c => c.Curp.ShouldBe("GOMA080515MSLRRNA9"), c => c.CurpValida.ShouldBeFalse());
        Filas(plan, "curps-invalidas.csv").Count.ShouldBe(2);
    }

    [Fact]
    public void Un_cupo_con_mas_citas_que_turnos_toma_la_capacidad_de_las_citas_y_se_reporta()
    {
        var plan = Planear(Datos(citas:
        [
            Cita(1, "PEGA000101MSLRRNA4"),
            Cita(2, "PEGA000102MSLRRNA2"),
            Cita(3, "PEGA000103MSLRRNA0"),
        ]));

        var cupo = plan.Cupos.ShouldHaveSingleItem();
        cupo.Turnos.ShouldBe((short)2);
        cupo.Capacidad.ShouldBe((short)3);
        Filas(plan, "sobrecupos.csv").ShouldHaveSingleItem();
    }

    [Fact]
    public void Los_totales_historicos_se_suman_por_oferta_y_los_que_no_tienen_oferta_se_reportan_por_mes()
    {
        var dia = new DateOnly(2025, 3, 10);
        var plan = Planear(Datos(
            tramites: [Tramite(10, 1), Tramite(13, 1)],
            historicos: [new(dia, 10, 4), new(dia, 13, 2), new(dia, null, 5), new(dia, 77, 1)]));

        plan.Historicos.ShouldHaveSingleItem().ShouldBe(new HistoricoPlan(dia, 10, 6));
        Filas(plan, "historico-sin-oferta.csv").ShouldHaveSingleItem().ShouldBe(["2025-03", "6"]);
    }

    [Fact]
    public void Los_usuarios_activos_con_correo_se_migran_con_su_rol_y_su_modulo()
    {
        var plan = Planear(Datos(usuarios:
        [
            new(1, "admin", "Ana", "Gómez", null, "admin@example.com", 2, 1, true),
            new(2, "captura", "Luis", "Pérez", null, "captura@example.com", 0, 1, true),
            new(3, "sin.modulo", "Eva", "Ruiz", null, "eva@example.com", 0, null, true),
            new(4, "baja", "Raúl", "Soto", null, "baja@example.com", 0, 1, false),
            new(5, "sin.correo", "Iris", "Paz", null, null, 1, 1, true),
        ]));

        plan.Usuarios.Select(u => (u.Usuario.Usuario, u.Rol, u.ModuloIdAnterior)).ShouldBe(
        [
            ("admin", "Administrador", (int?)null),
            ("captura", "Capturista", 1),
            ("sin.modulo", "Capturista", null),
        ]);
        Filas(plan, "usuarios.csv").Select(f => f[3]).ShouldContain("Se migra; asignar módulo en el panel");
        plan.TiposSinRol.ShouldBeEmpty();
    }

    [Fact]
    public void Un_tipo_de_usuario_fuera_del_mapa_se_informa_y_bloquea_la_migracion()
    {
        var plan = Planificador.Crear(
            Datos(usuarios: [new(1, "raro", "Ana", "Gómez", null, "a@example.com", 7, null, true)]),
            new OpcionesPlan(Corte, MapaRoles.Parsear("0=Capturista"), Decisiones.Vacias));

        plan.TiposSinRol.ShouldBe([7]);
        plan.Usuarios.ShouldBeEmpty();
    }

    [Fact]
    public void Un_modulo_sin_codigo_postal_se_migra_con_marcador_y_se_reporta_y_uno_de_municipio_inexistente_no()
    {
        var plan = Planear(Datos(modulos: [Modulo(1, cp: null), Modulo(2, municipio: 99)], tramites: [Tramite(10, 1)]));

        plan.Modulos.ShouldHaveSingleItem().CodigoPostal.ShouldBe("00000");
        Filas(plan, "modulos.csv").Count.ShouldBe(2);
    }

    [Fact]
    public void Las_horas_sospechosas_activas_se_reportan()
    {
        var plan = Planear(Datos(horarios: [new(100, 10, new TimeOnly(5, 0), 2, true), new(101, 10, new TimeOnly(4, 0), 2, false)]));

        Filas(plan, "horas-sospechosas.csv").ShouldHaveSingleItem()[4].ShouldBe("05:00");
    }
}
