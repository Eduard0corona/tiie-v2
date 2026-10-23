# 12 · Pruebas

Las reglas de agenda se prueban en el dominio con el reloj fijado; la concurrencia, las restricciones y la autorización se prueban contra un SQL Server real en contenedor; los flujos completos, con Playwright en tamaño de celular. Un PR no se integra si alguna falla.

## Niveles

| Nivel | Herramienta | Qué cubre | Cuándo corre |
| --- | --- | --- | --- |
| Dominio | xUnit, Shouldly | Reglas RN-xx, CURP, disponibilidad, transiciones de estado | Cada PR |
| Aplicación | xUnit, `FakeTimeProvider`, dobles de puertos | Casos de uso sin base de datos; arquitectura revisando las referencias de los ensamblados | Cada PR |
| Integración | `WebApplicationFactory`, Testcontainers (SQL Server), Respawn | Endpoints reales, SQL, índices, concurrencia, autorización, cabeceras | Cada PR (`Category=Integracion`) |
| Frontend | Vitest, Testing Library, MSW | `lib/`, componentes y flujos con API simulada | Cada PR |
| Migración | xUnit (`Tiie.Migracion.Tests`) y una prueba de integración con una base de 2016 de ejemplo | Reglas de mapeo de docs/14; migrar, repetir, conciliar y avisar contra SQL Server real | Cada PR |
| Humo | `tests/humo/humo.mjs` | Inicio, salud, disponibilidad y entrada al panel con MFA | Cada despliegue, en QA, en `staging` y en producción |
| Extremo a extremo | Playwright y axe | Flujos completos y accesibilidad contra QA | Cada despliegue a QA |
| Carga | k6 | Apertura de temporada y carga sostenida | Antes de la puerta de la Fase 2 y antes de cada temporada |

## Pruebas obligatorias

Cada fila es al menos una prueba con el ID en su nombre.

| Prueba | Nivel | Cubre |
| --- | --- | --- |
| 100 reservas simultáneas por el último lugar crean exactamente una cita | Integración | RN-02, RNF-04 |
| Dos reservas simultáneas de la misma CURP en cupos distintos crean una sola cita | Integración | RN-01 |
| La misma `Idempotency-Key` repetida devuelve la misma cita y ocupa un solo lugar | Integración | RF-03 |
| Reservar en domingo, día inhábil, fuera de la ventana u hoy (web) es rechazado | Dominio e integración | RN-03, RN-06, RN-07, RN-08 |
| A las 18:00 de Sinaloa (01:00 UTC del día siguiente) la ventana empieza mañana local | Dominio | RN-03, RNF-09 |
| Cupo de hoy con hora futura se acepta en ventanilla y se rechaza en la web | Dominio | RN-18 |
| CURP: los vectores de `tests/vectores-curp.json` dan el mismo resultado en C# y TypeScript | Dominio y frontend | RN-15 |
| El folio tiene formato `AAMMDD-NNNNNNN` con la fecha local y nunca se repite | Integración | RN-10 |
| Cancelar y reprogramar liberan y ocupan lugares en la misma transacción | Integración | RN-02, RN-17 |
| El ciudadano no puede cancelar el día de la cita; el personal sí | Dominio | RN-13 |
| Un token vencido o inexistente redirige a `enlace-invalido` | Integración | RN-16 |
| Ninguna ruta devuelve datos a partir de una CURP; la recuperación responde igual exista o no | Integración | RF-06, privacidad |
| Todo endpoint de `/api/admin` tiene política; anónimo recibe 401 | Integración | Autorización |
| Un capturista recibe 403 en rutas de supervisor y de administrador | Integración | Autorización |
| Personal del módulo A recibe 404 con recursos del módulo B | Integración | Alcance por módulo |
| Sesión sin MFA solo accede a `yo`, `mfa` y `salir` | Integración | MFA |
| `POST` sin `X-Tiie-Csrf` o con `Sec-Fetch-Site: cross-site` es rechazado | Integración | CSRF |
| Las respuestas llevan CSP, HSTS, `nosniff` y `Referrer-Policy` | Integración | Cabeceras |
| Un requisito de 5 000 caracteres se guarda y se imprime completo | Integración | RF-13 |
| El reporte del 1 al 31 incluye las citas del día 31 | Integración | RF-17 |
| Si el correo falla, la cita existe y la notificación se reintenta con el retraso esperado | Aplicación | RF-04 |
| Dos instancias del proceso de envío no mandan el mismo correo | Integración | Procesos |
| El generador de cupos es idempotente y no toca cupos con citas | Integración | Generación de cupos |
| El cierre diario solo afecta citas Agendada de fechas pasadas | Integración | RN-12 |
| Los logs de una reserva no contienen CURP, correo, celular ni nombre | Integración | Privacidad |
| `Tiie.Dominio` no referencia otros ensamblados; `Tiie.Aplicacion` no referencia `Infraestructura` ni `Web` | Aplicación | Arquitectura |

## Concurrencia

```csharp
[Fact, Trait("Category", "Integracion")]
public async Task RN02_cien_reservas_simultaneas_por_el_ultimo_lugar_crean_una_sola_cita()
{
    var cupo = await _semilla.CupoAsync(capacidad: 1);
    var solicitudes = Enumerable.Range(0, 100)
        .Select(i => _semilla.SolicitudCita(cupo.CupoId, GeneradorCurp.Valida(i)));

    var respuestas = await Task.WhenAll(solicitudes.Select(s => _cliente.ReservarAsync(s)));

    respuestas.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
    respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(99);
    (await _db.Cupos.AsNoTracking().SingleAsync(c => c.CupoId == cupo.CupoId)).Ocupados.ShouldBe(1);
}
```

## Reloj fijado

```csharp
[Fact]
public void RN03_a_las_18_horas_de_sinaloa_la_ventana_empieza_manana_local()
{
    var tiempo = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-03T01:00:00Z")); // 2 nov, 18:00 en Sinaloa
    var reloj = new RelojAgenda(tiempo, Opciones.Agenda());

    reloj.HoyLocal().ShouldBe(new DateOnly(2026, 11, 2));
    ReglasAgenda.PrimerDiaReservable(reloj.HoyLocal(), OrigenCita.Web).ShouldBe(new DateOnly(2026, 11, 3));
}
```

## Infraestructura de pruebas de integración

- `FabricaTiie : WebApplicationFactory<Program>` es un *assembly fixture* de xUnit: levanta un contenedor de SQL Server 2022 por ejecución, aplica las migraciones una vez y limpia los datos antes de cada prueba con Respawn (conserva `Rol` y `NivelEscolar`, que siembra la migración). Activa READ COMMITTED SNAPSHOT en la base, como lo trae Azure SQL, para que la concurrencia se pruebe con el mismo aislamiento que en producción. Corre en el entorno `Pruebas` y sin paralelismo (`xunit.runner.json`), porque todas las pruebas comparten la base.
- Reemplaza `TimeProvider` por `RelojPruebas`, que empieza cada prueba en el instante real y se adelanta con `Avanzar`; `FakeTimeProvider` no sirve aquí porque no puede regresar después de que una prueba adelantó horas. Reemplaza `ICorreo` por uno en memoria y agrega un proveedor de logs en memoria para revisar que no haya datos personales. Desde la Fase 2: `ICaptcha` por uno que acepta el token `valido` y sin `PlanificadorProcesos`; las pruebas de procesos los ejecutan directamente.
- El ensamblado declara `[assembly: Trait("Category", "Integracion")]`. Las pruebas rápidas se ejecutan con `dotnet test --filter-not-trait "Category=Integracion"`.
- En Docker Desktop para Windows, si Testcontainers no logra iniciar `testcontainers/ryuk`, se define `TESTCONTAINERS_RYUK_DISABLED=true`; la fábrica detiene su contenedor al terminar.
- `ClienteTiie` agrega `X-Tiie-Csrf: 1` y maneja cookies, y tiene ayudas para entrar como cada rol con MFA ya verificada.
- `GeneradorCurp.Valida(n)` produce CURPs ficticias con dígito verificador correcto.
- `tests/vectores-curp.json` es la fuente compartida de casos de CURP: `[{ "curp": "…", "valida": true, "fechaNacimiento": "2008-05-15", "sexo": "M" }]`.

## Matriz de autorización

Una prueba enumera `EndpointDataSource` y, para cada ruta de `/api/admin`:

1. Verifica que tenga metadatos de autorización.
2. La llama sin sesión y espera 401.
3. La llama con cada rol y compara el resultado con la matriz esperada, declarada en `MatrizPermisos.cs` a partir de [07](07-autenticacion-y-autorizacion.md#matriz-de-permisos).

Agregar un endpoint sin actualizar la matriz hace fallar esta prueba.

## Extremo a extremo

Playwright contra QA, en un perfil de celular (Pixel 7) y uno de escritorio:

1. Reserva completa: sede, fecha, hora, datos y Mi cita con el aviso de cita nueva.
2. Reserva con cupo lleno a mitad del flujo: mensaje y regreso al paso de hora.
3. CURP con cita activa: ofrece recuperar el enlace.
4. Enlace del correo (leído del buzón de prueba) → Mi cita → descargar PDF → cancelar.
5. Reprogramar desde Mi cita.
6. Personal: entrar con MFA, agenda del día, marcar asistencia.
7. Supervisor: editar una plantilla y ver el cambio en la disponibilidad pública.
8. Supervisor: crear un bloqueo con citas afectadas y cancelarlas con aviso.
9. Administrador: crear usuario, activar la cuenta y configurar MFA.

`@axe-core/playwright` revisa cada pantalla pública; un hallazgo grave o crítico falla la ejecución. En celular, además, ninguna pantalla puede desplazarse a lo ancho (WCAG 1.4.10).

Las pruebas están en `tests/e2e` ([README](../tests/e2e/README.md)). Crean sus propios datos (CURP nuevas, citas por la API) y corren una a la vez, porque comparten plantillas, cupos y bloqueos; lo que cambian lo restauran al terminar.

| Dónde | Cómo | Qué se omite |
| --- | --- | --- |
| Local | Sitio en Development con el correo `Archivo` y límites de solicitudes altos; `E2E_USUARIO` y `E2E_CONTRASENA` del administrador semilla (la primera corrida configura su MFA y guarda la clave en `tests/e2e/.auth/`); `E2E_CANAL=msedge` para usar el Edge instalado | El flujo 6 si no hay cupos de hoy: `GeneracionCupos` crea desde mañana, así que una base recién creada no los tiene |
| QA | Job `e2e` de `.github/workflows/despliegue.yml`, después de cada despliegue, con `E2E_USUARIO`, `E2E_CONTRASENA` y `E2E_TOTP` de un Administrador de pruebas. QA usa las claves de prueba de Turnstile | Los flujos 4 y 9, que leen el buzón de prueba (`E2E_CARPETA_CORREOS`): en QA el correo sale por Communication Services |

Sin usuario del panel se omiten los flujos 6 a 9. Producción no se despliega si el job `e2e` falla.

## Carga

Con k6 contra QA, escalado como producción:

| Escenario | Perfil | Criterio de éxito |
| --- | --- | --- |
| Apertura de temporada | Subida en 1 minuto hasta 2 veces el pico estimado, sobre pocas ofertas | Cero sobreventa; errores distintos de 409 menores a 1 %; reserva p95 menor a 1 s |
| Sostenido | El pico estimado durante 30 minutos | Disponibilidad p95 menor a 500 ms; CPU de SQL menor a 70 % |

El pico estimado sale de la consulta de volumen por día del análisis. Hasta tenerla, se usa 10 veces el promedio diario de 2025 concentrado en 2 horas.

Los escenarios son `tests/carga/apertura.js` y `tests/carga/sostenido.js` ([README](../tests/carga/README.md)). Durante la carga, QA usa las claves de prueba de Turnstile y límites de solicitudes altos, porque k6 sale de pocas IP. Con un Supervisor de pruebas (`K6_USUARIO`, `K6_CONTRASENA`, `K6_TOTP`), la apertura termina revisando cada cupo de la ventana: `Ocupados` nunca mayor que `Capacidad`.

## Puertas del CI

| Puerta | Falla si |
| --- | --- |
| Compilación | Advertencias o `dotnet format --verify-no-changes` con cambios |
| Pruebas .NET | Cualquier prueba falla |
| Cobertura | `Tiie.Dominio` por debajo de 90 % o `Tiie.Aplicacion` por debajo de 80 % de líneas, sumando todos los proyectos de prueba (`tests/verificar-cobertura.mjs`; el código generado no cuenta, `tests/cobertura.config`) |
| Frontend | Lint, tipos o pruebas fallan |
| Contrato | `src/api/esquema.d.ts` no coincide con el OpenAPI generado |
| Tamaño | JavaScript inicial del sitio público por encima de 200 KB comprimido |
| Seguridad | Secreto detectado; paquete con vulnerabilidad alta o crítica |
| Migraciones | Hay cambios de modelo sin migración (`dotnet ef migrations has-pending-model-changes`) |
