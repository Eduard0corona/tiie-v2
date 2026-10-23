using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Catalogos;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Infraestructura.Datos.Semilla;

/// <summary>Sección <c>Semilla</c>: solo Development, con <c>dotnet user-secrets</c>.</summary>
public sealed class OpcionesSemilla
{
    public const string Seccion = "Semilla";

    public string UsuarioAdministrador { get; set; } = "admin";
    public string CorreoAdministrador { get; set; } = "admin@tiie.localhost";
    public string? ContrasenaAdministrador { get; set; }
}

/// <summary>Carga datos de ejemplo en Development y QA. Nunca se ejecuta en producción.</summary>
public interface ISembradorDesarrollo
{
    Task SembrarAsync(CancellationToken ct);
}

/// <summary>
/// Catálogos de ejemplo (Sinaloa, tipos de trámite, dos módulos con ofertas y plantillas) y el
/// administrador semilla. Cada parte se siembra solo si su tabla está vacía; es seguro repetirlo.
/// </summary>
public sealed class SembradorDesarrollo(
    TiieDbContext db,
    UserManager<Usuario> usuarios,
    IOptions<OpcionesSemilla> semilla,
    IOptions<OpcionesAgenda> agenda,
    ILogger<SembradorDesarrollo> logger) : ISembradorDesarrollo
{
    public async Task SembrarAsync(CancellationToken ct)
    {
        if (!await db.Database.CanConnectAsync(ct))
        {
            logger.LogWarning("Semilla omitida: no hay conexión con la base de datos.");
            return;
        }
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            logger.LogWarning("Semilla omitida: hay migraciones pendientes. Ejecuta dotnet ef database update.");
            return;
        }

        await SembrarSinaloaAsync(ct);
        await SembrarTiposTramiteAsync(ct);
        await SembrarModulosAsync(ct);
        await SembrarAdministradorAsync();
    }

    private async Task SembrarSinaloaAsync(CancellationToken ct)
    {
        if (await db.Entidades.AnyAsync(ct))
            return;

        // Claves municipales del INEGI; en producción los IDs vienen de la migración de datos.
        string[] municipios =
        [
            "Ahome", "Angostura", "Badiraguato", "Concordia", "Cosalá", "Culiacán", "Choix", "Elota", "Escuinapa",
            "El Fuerte", "Guasave", "Mazatlán", "Mocorito", "Rosario", "Salvador Alvarado", "San Ignacio", "Sinaloa",
            "Navolato", "Eldorado", "Juan José Ríos",
        ];
        var sinaloa = new Entidad { EntidadId = Entidad.Sinaloa, Nombre = "Sinaloa" };
        for (var i = 0; i < municipios.Length; i++)
            sinaloa.Municipios.Add(new Municipio { MunicipioId = (short)(i + 1), Nombre = municipios[i] });

        db.Entidades.Add(sinaloa);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Semilla: entidad 25 con {Municipios} municipios.", municipios.Length);
    }

    private async Task SembrarTiposTramiteAsync(CancellationToken ct)
    {
        if (await db.TiposTramite.AnyAsync(ct))
            return;

        var primeraVez = new TipoTramite
        {
            Nombre = "Primera vez",
            Descripcion = "Estudiantes que nunca han tenido TIIE.",
            Costo = 0,
            Notas = "Preséntate 10 minutos antes de tu cita con tus documentos originales.",
        };
        Requisitos(primeraVez,
            (null, "CURP impresa."),
            (NivelEscolar.Basico, "Constancia de estudios del ciclo vigente con fotografía, firmada y sellada por la escuela."),
            (NivelEscolar.Basico, "Acudir con madre, padre o tutor, con identificación oficial."),
            (NivelEscolar.MedioSuperiorYSuperior, "Ficha de inscripción o comprobante de estudios del ciclo vigente."),
            (NivelEscolar.MedioSuperiorYSuperior, "Identificación con fotografía."));

        var sustitucion = new TipoTramite
        {
            Nombre = "Sustitución",
            Descripcion = "Reposición por pérdida, robo o daño de la tarjeta.",
            Costo = 150,
            Notas = "El pago se realiza en el módulo.",
        };
        Requisitos(sustitucion,
            (null, "CURP impresa."),
            (null, "Comprobante de estudios del ciclo vigente."));

        var reactivacion = new TipoTramite
        {
            Nombre = "Reactivación",
            Descripcion = "Estudiantes con TIIE de un ciclo anterior que siguen estudiando.",
            Costo = 0,
        };
        Requisitos(reactivacion,
            (null, "Tarjeta TIIE anterior."),
            (null, "Comprobante de estudios del ciclo vigente."));

        db.TiposTramite.AddRange(primeraVez, sustitucion, reactivacion);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Semilla: 3 tipos de trámite con requisitos.");
    }

    private async Task SembrarModulosAsync(CancellationToken ct)
    {
        if (await db.Modulos.AnyAsync(ct))
            return;

        var culiacan = new Modulo
        {
            MunicipioId = 6,
            Nombre = "Módulo Culiacán Centro",
            Calle = "Av. Álvaro Obregón",
            NumeroExterior = "123",
            Colonia = "Centro",
            CodigoPostal = "80000",
            Localidad = "Culiacán Rosales",
            Telefono = "6670000000",
            HorarioAtencion = "Lunes a viernes de 8:00 a 15:00, sábado de 9:00 a 12:30",
            Latitud = 24.806m,
            Longitud = -107.394m,
        };
        var mazatlan = new Modulo
        {
            MunicipioId = 12,
            Nombre = "Módulo Mazatlán",
            Calle = "Av. Ejército Mexicano",
            NumeroExterior = "1000",
            Colonia = "Centro",
            CodigoPostal = "82000",
            Localidad = "Mazatlán",
            HorarioAtencion = "Lunes a viernes de 8:00 a 15:00, sábado de 9:00 a 12:30",
            Latitud = 23.232m,
            Longitud = -106.420m,
        };

        var tipos = await db.TiposTramite.OrderBy(t => t.TipoTramiteId).ToListAsync(ct);
        foreach (var modulo in new[] { culiacan, mazatlan })
        {
            foreach (var tipo in tipos)
            {
                var oferta = new Oferta { TipoTramite = tipo, DiasVentana = agenda.Value.DiasVentanaPredeterminados };
                // RN-05: la reactivación en Mazatlán no atiende en sábado.
                var atiendeSabado = !(modulo == mazatlan && tipo.Nombre == "Reactivación");
                Plantilla(oferta, atiendeSabado);
                modulo.Ofertas.Add(oferta);
            }
        }

        db.Modulos.AddRange(culiacan, mazatlan);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Semilla: 2 módulos con {Ofertas} ofertas y sus plantillas.", tipos.Count * 2);
    }

    private async Task SembrarAdministradorAsync()
    {
        if ((await usuarios.GetUsersInRoleAsync(Roles.Administrador)).Count > 0)
            return;

        var opciones = semilla.Value;
        if (string.IsNullOrWhiteSpace(opciones.ContrasenaAdministrador))
        {
            logger.LogWarning(
                "Semilla: no se creó el administrador porque falta Semilla:ContrasenaAdministrador en user-secrets.");
            return;
        }

        var admin = new Usuario
        {
            UserName = opciones.UsuarioAdministrador,
            Email = opciones.CorreoAdministrador,
            EmailConfirmed = true,
            Nombre = "Administrador",
            ApellidoPaterno = "Semilla",
        };
        var creado = await usuarios.CreateAsync(admin, opciones.ContrasenaAdministrador);
        if (!creado.Succeeded)
        {
            logger.LogError("Semilla: no se pudo crear el administrador: {Errores}",
                string.Join(" ", creado.Errors.Select(e => e.Description)));
            return;
        }
        await usuarios.AddToRoleAsync(admin, Roles.Administrador);
        logger.LogInformation("Semilla: administrador {Usuario} creado; configurará MFA al entrar.", admin.UserName);
    }

    private static void Requisitos(TipoTramite tipo, params (byte? Nivel, string Texto)[] requisitos)
    {
        for (var i = 0; i < requisitos.Length; i++)
        {
            tipo.Requisitos.Add(new Requisito
            {
                NivelEscolarId = requisitos[i].Nivel,
                Texto = requisitos[i].Texto,
                Orden = (short)(i + 1),
            });
        }
    }

    /// <summary>Lunes a viernes de 8:00 a 14:30 con 4 turnos; sábado de 9:00 a 12:00 con 3.</summary>
    private static void Plantilla(Oferta oferta, bool atiendeSabado)
    {
        for (var dia = DayOfWeek.Monday; dia <= DayOfWeek.Friday; dia++)
        {
            for (var hora = new TimeOnly(8, 0); hora <= new TimeOnly(14, 30); hora = hora.AddMinutes(30))
                oferta.Plantilla.Add(new PlantillaHorario { DiaSemana = dia, Hora = hora, Turnos = 4 });
        }
        if (!atiendeSabado)
            return;
        for (var hora = new TimeOnly(9, 0); hora <= new TimeOnly(12, 0); hora = hora.AddMinutes(30))
            oferta.Plantilla.Add(new PlantillaHorario { DiaSemana = DayOfWeek.Saturday, Hora = hora, Turnos = 3 });
    }
}
