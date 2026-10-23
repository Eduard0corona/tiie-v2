# 09 · Backend

ASP.NET Core 10 con minimal APIs, organizado por característica. Las reglas viven en `Tiie.Dominio`; los casos de uso en `Tiie.Aplicacion` orquestan reglas, datos y puertos; `Tiie.Web` solo traduce HTTP.

## Configuración común

`Directory.Build.props` en la raíz:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

- Las versiones de paquetes van en `Directory.Packages.props` (gestión central de paquetes).
- `InvariantGlobalization` queda en `false` porque la zona `America/Mazatlan` necesita los datos de zonas horarias del sistema.
- Estilo: `.editorconfig` en la raíz; CI ejecuta `dotnet format --verify-no-changes`.

## Proyectos y paquetes

| Proyecto | Paquetes principales |
| --- | --- |
| `Tiie.Dominio` | Ninguno |
| `Tiie.Aplicacion` | `Microsoft.EntityFrameworkCore.Relational`, `FluentValidation`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Options.ConfigurationExtensions` y `.DataAnnotations` (opciones validadas al iniciar) |
| `Tiie.Infraestructura` | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Azure.Identity`, `Azure.Communication.Email`, `QuestPDF`, `ClosedXML`, `Azure.Extensions.AspNetCore.DataProtection.Blobs`, `Azure.Extensions.AspNetCore.DataProtection.Keys` |
| `Tiie.Web` | `Microsoft.AspNetCore.OpenApi`, `Microsoft.Extensions.ApiDescription.Server`, `Azure.Monitor.OpenTelemetry.AspNetCore`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` |
| Pruebas | `xunit.v3` (con Microsoft.Testing.Platform, ver `global.json`), `Shouldly`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MsSql`, `Respawn`, `Microsoft.Extensions.TimeProvider.Testing`. Las reglas de arquitectura revisan las referencias de cada ensamblado con reflexión, sin `NetArchTest` |

`Directory.Packages.props` también fija dependencias transitivas con avisos de seguridad (`CentralPackageTransitivePinningEnabled`), por ejemplo `System.Security.Cryptography.Pkcs`, que `Microsoft.Data.SqlClient` trae en una versión vulnerable.

QuestPDF exige licencia según el tipo y tamaño de la organización: validarla antes de la Fase 2. Si no procede, el comprobante se genera como página imprimible en React y el correo lleva solo el enlace.

## Estructura

```text
src/
├─ Tiie.Dominio/
│  ├─ Agenda/            Cupo, Oferta, PlantillaHorario, Bloqueo, ReglasAgenda, CalculadoraDisponibilidad
│  ├─ Citas/             Cita, EstadoCita, OrigenCita, Folio
│  ├─ Solicitantes/      Solicitante, Curp
│  ├─ Catalogos/         Modulo, TipoTramite, Requisito, Municipio, NivelEscolar
│  └─ Errores/           ReglaNegocioException, CodigoError
├─ Tiie.Aplicacion/
│  ├─ Puertos/           ITiieDb, ICorreo, IGeneradorPdf, IGeneradorExcel, ICaptcha, ITokensCita
│  ├─ Reloj/             RelojAgenda
│  ├─ Publico/           ConsultarDisponibilidad, ConsultarCupos, ConsultarCatalogoPublico
│  ├─ Reservas/          CrearCita, RecuperarEnlace
│  ├─ MiCita/            ConsultarMiCita, CancelarMiCita, ReprogramarMiCita
│  ├─ Agenda/            ConsultarAgenda, RegistrarAsistencia
│  ├─ Citas/             BuscarCitas, CitaVentanilla, CancelarCita, ReprogramarCita
│  ├─ Catalogos/         Modulos, TiposTramite, Ofertas
│  ├─ Horarios/          GuardarPlantilla, GeneradorCupos, Bloqueos
│  ├─ Usuarios/          CrearUsuario, ActualizarUsuario
│  ├─ Reportes/          ReporteCitas, Tablero
│  └─ Procesos/          EnvioNotificaciones, GeneracionCupos, CierreDiario, Recordatorios, Retencion
├─ Tiie.Infraestructura/
│  ├─ Datos/             TiieDbContext, Configuraciones/, Migraciones/, Interceptores/, Semilla/
│  ├─ Identidad/         Usuario, Rol, FabricaClaimsUsuario, ValidadorContrasenasComunes
│  ├─ Correo/            CorreoAcs, CorreoArchivo, Plantillas/
│  ├─ Documentos/        ComprobantePdf, ReporteExcel
│  └─ Captcha/           CaptchaTurnstile
└─ Tiie.Web/
   ├─ Program.cs
   ├─ Endpoints/         PublicoEndpoints, MiCitaEndpoints, AdminAuthEndpoints, AdminCitasEndpoints, …
   ├─ Seguridad/         políticas, cookies, FiltroCsrf, AutorizacionModulo, AccesoCita
   ├─ Errores/           ManejadorExcepciones, ErroresApi, CatalogoErrores, ProblemasApi
   └─ Infraestructura/   cabeceras, límites, observabilidad
```

## Casos de uso

Un caso de uso es un `record` de entrada, un validador y un manejador con un método `EjecutarAsync`. No se usa MediatR: el manejador se inyecta directamente en el endpoint.

```csharp
namespace Tiie.Aplicacion.Reservas;

public sealed record CrearCita(
    int CupoId, string Curp, string Nombre, string ApellidoPaterno, string? ApellidoMaterno,
    string? Celular, string? Correo, bool AceptaAvisoPrivacidad,
    Guid ClaveIdempotencia, OrigenCita Origen, int? UsuarioId);

public sealed class CrearCitaValidador : AbstractValidator<CrearCita>
{
    public CrearCitaValidador()
    {
        RuleFor(x => x.Curp).Must(Curp.EsValida).WithMessage("La CURP no es válida.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ApellidoPaterno).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().When(x => x.Origen == OrigenCita.Web);
        RuleFor(x => x.Celular).Matches("^[0-9]{10}$").When(x => x.Origen == OrigenCita.Web || x.Celular is not null);
        RuleFor(x => x.AceptaAvisoPrivacidad).Equal(true);
    }
}
```

### La reserva, paso a paso

```csharp
public sealed class CrearCitaManejador(
    ITiieDb db, RelojAgenda reloj, ITokensCita tokens, IValidator<CrearCita> validador, IOptions<OpcionesSitio> sitio)
{
    public async Task<CitaCreada> EjecutarAsync(CrearCita cmd, CancellationToken ct)
    {
        await validador.ValidateAndThrowAsync(cmd, ct);                // 422 Validacion

        // Reintento con la misma clave: devolver la cita ya creada.
        var previa = await db.Citas.AsNoTracking()
            .Where(c => c.ClaveIdempotencia == cmd.ClaveIdempotencia)
            .Select(CitaCreada.Proyeccion).SingleOrDefaultAsync(ct);
        if (previa is not null) return previa;

        var cupo = await db.Cupos.AsNoTracking()
            .Where(c => c.CupoId == cmd.CupoId)
            .Select(c => new { c.Fecha, c.Hora, c.Cerrado, c.Oferta.Activo, ModuloActivo = c.Oferta.Modulo.Activo,
                               TipoActivo = c.Oferta.TipoTramite.Activo, c.Oferta.DiasVentana })
            .SingleOrDefaultAsync(ct) ?? throw new NoEncontradoException();

        ReglasAgenda.ValidarReserva(cupo.Fecha, cupo.Hora, cupo.DiasVentana, cupo.Cerrado,
            ofertaActiva: cupo.Activo && cupo.ModuloActivo && cupo.TipoActivo,
            origen: cmd.Origen, ahora: reloj.AhoraLocal());          // RN-03, RN-07, RN-09, RN-18

        var datosCurp = Curp.Interpretar(cmd.Curp);                    // RN-15

        var estrategia = db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var filas = await db.Cupos
                .Where(c => c.CupoId == cmd.CupoId && !c.Cerrado && c.Ocupados < c.Capacidad)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ocupados, c => c.Ocupados + 1), ct);
            if (filas == 0) throw new ReglaNegocioException(CodigoError.SinCupo);   // RN-02

            var solicitante = await Solicitantes.ObtenerOActualizarAsync(db, datosCurp, cmd, reloj, ct);
            var folio = await Folios.SiguienteAsync(db, reloj.HoyLocal(), ct);       // RN-10
            var token = tokens.Emitir(cupo.Fecha);                                   // RN-16

            var cita = Cita.Agendar(folio, cmd.CupoId, solicitante, token, cmd, reloj.AhoraUtc(),
                sitio.Value.VersionAvisoPrivacidad);
            db.Citas.Add(cita);
            if (solicitante.Correo is not null)
                db.Notificaciones.Add(Notificacion.Confirmacion(cita, solicitante.Correo, reloj.AhoraUtc()));

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.ViolaIndice("UX_Cita_SolicitanteActiva"))
            {
                throw new ReglaNegocioException(CodigoError.YaTieneCita);           // RN-01
            }

            await tx.CommitAsync(ct);
            return CitaCreada.Desde(cita, cupo.Fecha, cupo.Hora);
        });
    }
}
```

Reglas de este patrón:

- La transacción va dentro de `CreateExecutionStrategy().ExecuteAsync`, porque la conexión usa `EnableRetryOnFailure`. Todo lo que se agrega al contexto se crea dentro de la lambda, para que un reintento empiece limpio.
- El cupo se ocupa con `ExecuteUpdateAsync`: una sola sentencia `UPDATE` atómica. Nunca se carga el cupo, se compara y se guarda.
- Si dos peticiones con la misma `ClaveIdempotencia` llegan a la vez, una gana y la otra choca con el índice único de la clave; ese caso se resuelve devolviendo la cita existente.
- Si dos reservas simultáneas traen la misma CURP nueva, la segunda choca con `UX_Solicitante_Curp` al crear el solicitante; también se responde `YaTieneCita`.
- `ViolaIndice` revisa `SqlException.Number` 2601 o 2627 y el nombre del índice en el mensaje.
- El manejador valida su propia entrada. Así la misma validación aplica a la reserva pública y a la de ventanilla, sin depender del endpoint.

## Endpoints

Cada grupo aplica de una vez su política, el filtro CSRF, el límite de solicitudes y la etiqueta de OpenAPI:

```csharp
public static class PublicoEndpoints
{
    public static void MapEndpointsPublicos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/publico")
            .WithTags("Público")
            .AllowAnonymous()
            .AddEndpointFilter<FiltroCsrf>()
            .RequireRateLimiting(Limites.PublicoLectura);

        g.MapGet("/ofertas/{ofertaId:int}/disponibilidad", async (int ofertaId, DateOnly? desde, DateOnly? hasta,
                ConsultarDisponibilidadManejador m, CancellationToken ct) =>
            TypedResults.Ok(await m.EjecutarAsync(new(ofertaId, desde, hasta), ct)));

        g.MapPost("/citas", async (SolicitudCita body, [FromHeader(Name = "Idempotency-Key")] Guid clave,
                ICaptcha captcha, CrearCitaManejador m, AccesoCita acceso, HttpContext http, CancellationToken ct) =>
            {
                await captcha.ExigirValidoAsync(body.TokenCaptcha, http.Connection.RemoteIpAddress, ct);
                var creada = await m.EjecutarAsync(body.ACrearCita(clave), ct);
                await acceso.IniciarSesionAsync(http, creada.CitaId, creada.TokenVenceEn);
                return TypedResults.Created("/api/mi-cita", creada.Resumen);
            })
            .RequireRateLimiting(Limites.Reserva);
    }
}
```

- La validación no vive en el endpoint: el manejador lanza `ValidationException` y `ManejadorExcepciones` responde 422 `Validacion` con `errors`.
- Los endpoints devuelven `TypedResults` para que OpenAPI documente cada respuesta.
- `/api/admin` se divide en subgrupos por política (`Personal`, `Supervisor`, `Administrador`). Una prueba recorre `EndpointDataSource` y falla si un endpoint de `/api/admin` no tiene metadatos de autorización.

## Errores

| Excepción | HTTP | `codigo` |
| --- | --- | --- |
| `ReglaNegocioException(codigo)` | Según el código (409 o 422) | El de la excepción |
| `ValidationException` (FluentValidation) | 422 | `Validacion` |
| `NoEncontradoException` | 404 | `NoEncontrado` |
| `DbUpdateConcurrencyException` | 409 | `Conflicto` |
| `CaptchaInvalidoException` | 422 | `CaptchaInvalido` |
| `BadHttpRequestException` (JSON mal formado, falta `Idempotency-Key`) | 400 | `SolicitudInvalida` |
| Cualquier otra | 500 | `ErrorInterno`, sin detalles; se registra con su traza |

`ManejadorExcepciones : IExceptionHandler` hace la traducción con `IProblemDetailsService`; `CatalogoErrores` define el código HTTP y el título en español de cada `codigo`, y `ProblemasApi` agrega `traceId` a toda respuesta de error y completa `codigo` cuando falta (un 405 o un 404 de archivos, por ejemplo). Los endpoints responden errores con `ErroresApi`. Sin sesión, `ManejadorResultadoAutorizacion` responde 401 `NoAutenticado`; sin permiso, 403 `Prohibido`, o `MfaRequerido` si la sesión aún no configura MFA. El catálogo completo de códigos está en [06](06-api.md#errores).

## EF Core

- `TiieDbContext` hereda de `IdentityDbContext<Usuario, Rol, int>` e implementa `ITiieDb`, la interfaz que usan los casos de uso.
- Conexión con `EnableRetryOnFailure()` y `CommandTimeout` de 30 s.
- Interceptores:
  - `MarcasDeTiempoInterceptor`: llena `CreadoEn` y `ModificadoEn` con `TimeProvider`.
  - `AuditoriaInterceptor`: escribe en `Auditoria` los cambios del personal sobre catálogos, plantillas, bloqueos, usuarios y estados de cita. Enmascara datos personales.
- Lecturas con `AsNoTracking()` y proyección directa a DTO; sin carga diferida (*lazy loading*).
- Concurrencia optimista con `RowVersion` en las entidades que edita el personal; el DTO la viaja como `version`.
- Migraciones en `Tiie.Infraestructura/Datos/Migraciones`, con nombres descriptivos en español (`AgregaIndiceCitaFolioAnterior`).

## Reloj

```csharp
public sealed class RelojAgenda(TimeProvider tiempo, IOptions<OpcionesAgenda> opciones)
{
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById(opciones.Value.ZonaHoraria);

    public DateTimeOffset AhoraUtc() => tiempo.GetUtcNow();
    public DateTime AhoraLocal() => TimeZoneInfo.ConvertTime(tiempo.GetUtcNow(), _zona).DateTime;
    public DateOnly HoyLocal() => DateOnly.FromDateTime(AhoraLocal());
    public DateTimeOffset FinDelDia(DateOnly fecha)
    {
        var local = fecha.ToDateTime(new TimeOnly(23, 59, 59));
        return new DateTimeOffset(local, _zona.GetUtcOffset(local)).ToUniversalTime();
    }
}
```

`TimeProvider.System` se registra en producción; las pruebas unitarias usan `FakeTimeProvider` para fijar cualquier instante y las de integración un reloj que puede volver al instante real entre pruebas ([12](12-pruebas.md#infraestructura-de-pruebas-de-integración)).

## Logging y observabilidad

- `ILogger` con mensajes estructurados: `logger.LogInformation("Cita {Folio} creada en el cupo {CupoId}", folio, cupoId)`.
- Cada petición que toca una cita abre un *scope* con `Folio`, para rastrearla en Application Insights.
- Nunca se registra el cuerpo de una petición, ni CURP, nombre, correo o celular sin `Enmascarar`.
- OpenTelemetry con `UseAzureMonitor()` envía trazas, métricas y logs. Métricas propias: `tiie.reservas`, `tiie.reservas.rechazadas` (por `codigo`), `tiie.notificaciones.pendientes`, `tiie.procesos.duracion`.

## OpenAPI

El documento se genera al compilar para que la SPA genere sus tipos:

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../openapi</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
</PropertyGroup>
```

La generación arranca la aplicación sin servirla. Por eso `Program.cs` no registra procesos en segundo plano ni ejecuta la semilla cuando el proceso de entrada es la herramienta de OpenAPI (`Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"`). En ese caso arranca en Development con una cadena de conexión ficticia, porque la validación de opciones al iniciar sí se ejecuta y la herramienta nunca abre la base.

`DocumentoOpenApi` ajusta el documento: horas como `HH:mm`, enumeraciones como texto, números sin variante de texto (`JsonNumberHandling.Strict`) y el esquema de error `ProblemaApi` como respuesta `default` de cada operación.

## Opciones y arranque

- Cada sección de configuración tiene su clase (`OpcionesAgenda`, `OpcionesSitio`, `OpcionesCorreo`, `OpcionesCaptcha`, `OpcionesDataProtection`) con `ValidateDataAnnotations()` y `ValidateOnStart()`.
- En Development y QA, `ISembradorDesarrollo` crea catálogos de ejemplo y el administrador semilla si la base está vacía.
- `/salud` comprueba el `DbContext`; App Service lo usa como *health check* y el cambio de ranura espera a que responda 200.
