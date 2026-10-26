# CLAUDE.md

Guía para agentes de IA y para cualquier persona que escriba código en este repositorio. Antes de cambiar algo, lee [docs/03-reglas-de-negocio.md](docs/03-reglas-de-negocio.md) y [docs/04-arquitectura.md](docs/04-arquitectura.md).

## Qué es

Un solo sitio ASP.NET Core 10 que sirve una SPA de React. La parte pública agenda citas de la TIIE; `/admin` es el panel del personal. El personal entra con ASP.NET Core Identity (cookie y MFA obligatoria). El ciudadano no tiene cuenta: accede a su cita con un token que recibe por correo.

## Reglas que no se rompen

1. **Las reglas de agenda viven en el backend.** `Tiie.Dominio` y `Tiie.Aplicacion` deciden si una reserva, cancelación o reprogramación es válida. Los endpoints solo traducen HTTP. React valida formato para dar retroalimentación, nunca reglas de negocio.
2. **Nada de reloj del sistema.** No uses `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` ni `new Date()` para reglas. En .NET inyecta `TimeProvider` y usa `RelojAgenda` (zona `America/Mazatlan`). En React usa `src/lib/fechas.ts`.
3. **Tipos de fecha.** `DateOnly` para fechas de cita, `TimeOnly` para horas, `DateTimeOffset` en UTC para instantes. En SQL: `date`, `time(0)` y `datetimeoffset(0)`. La API envía fechas como texto `yyyy-MM-dd` y horas como `HH:mm`.
4. **Cupos atómicos.** Reservar, cancelar y reprogramar cambian `Cupo.Ocupados` con un `ExecuteUpdateAsync` condicional dentro de la misma transacción que la cita. Está prohibido leer `Ocupados`, compararlo en C# y luego escribir.
5. **Privacidad.** Ningún endpoint devuelve datos de una cita a partir de una CURP. El ciudadano solo accede con la cookie `tiie_cita`, que se obtiene con el token.
6. **Autorización en cada endpoint de `/api/admin`.** Política de rol en el grupo y, si el recurso pertenece a un módulo, `IAutorizacionModulo.ExigirAcceso(moduloId)`. Una prueba automática falla si un endpoint de `/api/admin` no tiene política.
7. **Sin datos personales en logs.** CURP, nombre, correo y celular se registran solo enmascarados con `Enmascarar`.
8. **Sin secretos en el repositorio.** Local: `dotnet user-secrets`. Azure: Key Vault con identidad administrada.
9. **Cada regla `RN-xx` tiene pruebas** y el nombre de la prueba incluye el ID, por ejemplo `RN02_no_permite_reservar_si_el_cupo_esta_lleno`.
10. **Esquema solo por migraciones de EF Core.** No edites una migración que ya se aplicó en QA; crea otra. Las migraciones deben ser compatibles con la versión anterior del código (expandir y luego contraer).
11. **Dependencias nuevas** de NuGet o npm se justifican en el PR. Un cambio de arquitectura necesita un ADR en `docs/adr/`.

## Convenciones

- **Idioma:** dominio, API e interfaz en español. Identificadores sin acentos ni ñ (`Contrasena`, `Anio`); textos visibles con ortografía completa.
- **C#:** nullable habilitado y advertencias como errores; clases `sealed` por defecto; DTOs como `record`; código organizado por característica (`Reservas/CrearCita.cs`), no por tipo técnico.
- **API:** prefijos `/api/publico`, `/api/mi-cita` y `/api/admin`; JSON en camelCase; errores como Problem Details con la extensión `codigo` (catálogo en [docs/06-api.md](docs/06-api.md)). Toda petición que modifica datos envía la cabecera `X-Tiie-Csrf: 1`.
- **React:** carpetas `features/publico` y `features/admin`; consultas con TanStack Query; formularios con React Hook Form y Zod; tipos de la API **generados** con `npm run api:tipos`, nunca escritos a mano.
- **Commits:** Conventional Commits en español (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).

## Comandos

| Tarea | Comando |
| --- | --- |
| Compilar | `dotnet build` |
| Pruebas .NET | `dotnet test` |
| Pruebas rápidas, sin contenedores | `dotnet test --filter-not-trait "Category=Integracion"` |
| Frontend en desarrollo | `npm run dev` en `src/Tiie.Web.Client` |
| Verificación del frontend | `npm run lint && npm run typecheck && npm test` |
| Tipos de la API | `npm run api:tipos` (requiere `dotnet build` previo) |
| Migración nueva | `dotnet ef migrations add <Nombre> --project src/Tiie.Infraestructura --startup-project src/Tiie.Web` |

## Cómo agregar un endpoint

1. Crea el caso de uso en `src/Tiie.Aplicacion/<Caracteristica>/`: el `record` de entrada, su validador y el manejador.
2. Mapéalo en `src/Tiie.Web/Endpoints/<Grupo>Endpoints.cs`, dentro del grupo correcto. El grupo ya aplica política, protección CSRF y límite de solicitudes.
3. Escribe la prueba de integración: caso exitoso, validación (422) y acceso denegado (401 o 403).
4. Ejecuta `npm run api:tipos` y consúmelo desde React con un hook en la carpeta de la característica.
5. Actualiza [docs/06-api.md](docs/06-api.md).

## Dónde está cada cosa

| Necesito… | Archivo o carpeta |
| --- | --- |
| Reglas de reserva | `src/Tiie.Dominio/Agenda/` |
| Casos de uso | `src/Tiie.Aplicacion/` |
| `DbContext`, configuraciones y migraciones | `src/Tiie.Infraestructura/Datos/` |
| Identity, cookies y políticas | `src/Tiie.Web/Seguridad/` |
| Procesos en segundo plano | `src/Tiie.Aplicacion/Procesos/`, registrados en `src/Tiie.Web/Program.cs` |
| Cliente HTTP del frontend | `src/Tiie.Web.Client/src/api/` |
| Fechas y zona horaria en React | `src/Tiie.Web.Client/src/lib/fechas.ts` |
| Contenido editable de la portada | `src/Tiie.Aplicacion/Sitio/` y `src/Tiie.Web.Client/src/features/admin/portada/` (enlaces: `src/Tiie.Dominio/Sitio/ReglasPortada.cs` y `src/Tiie.Web.Client/src/lib/enlaces.ts`) |
| Validación de CURP (ambos lados) | `src/Tiie.Dominio/Solicitantes/Curp.cs` y `src/Tiie.Web.Client/src/lib/curp.ts` |
| Recursos de Azure | `infra/` (Bicep) |
| Despliegue | `.github/workflows/despliegue.yml` y `tests/humo/humo.mjs` |
| Migración desde 2016 | `tools/Tiie.Migracion/` (el esquema de origen es supuesto: `Origen/esquema-supuesto.sql`) |
