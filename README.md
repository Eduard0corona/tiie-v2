# TIIE v2 · Agenda de citas

Sistema para agendar citas presenciales del trámite de la Tarjeta Inteligente de Identidad Estudiantil (TIIE) en los módulos de atención de Sinaloa. Reemplaza al sistema de 2016 (ASP.NET WebForms) con un solo sitio web: la página pública para el ciudadano y el panel de administración para el personal.

> **Estado:** Fase 1 en curso. Ya están el esqueleto, el modelo de datos con su primera migración, la identidad del personal con MFA y el CI; falta la infraestructura de Azure (épica E5). El avance por historia está en el [plan de implementación](docs/15-plan-de-implementacion.md#fase-1--fundaciones).

## Stack

| Capa | Tecnología |
| --- | --- |
| Backend | ASP.NET Core 10 con minimal APIs, EF Core 10, ASP.NET Core Identity |
| Frontend | React 19 con TypeScript, Vite, React Router 7, TanStack Query 5, Tailwind CSS 4 |
| Base de datos | Azure SQL Database; SQL Server 2022 en contenedor para desarrollo |
| Nube | Azure App Service (Linux), Key Vault, Storage, Communication Services Email, Application Insights |
| Pruebas | xUnit, Testcontainers, Vitest, Testing Library, Playwright |

## Estructura

```text
tiie-v2/
├─ src/
│  ├─ Tiie.Dominio/            entidades y reglas de agenda; sin dependencias externas
│  ├─ Tiie.Aplicacion/         casos de uso, puertos y procesos en segundo plano
│  ├─ Tiie.Infraestructura/    EF Core, Identity, correo, PDF, captcha
│  ├─ Tiie.Web/                host ASP.NET Core: API, autenticación y archivos de la SPA
│  └─ Tiie.Web.Client/         aplicación React: sitio público y /admin
├─ tests/
│  ├─ Tiie.Dominio.Tests/
│  ├─ Tiie.Aplicacion.Tests/
│  ├─ Tiie.Integracion.Tests/  WebApplicationFactory + SQL Server en contenedor
│  └─ e2e/                     Playwright
├─ tools/Tiie.Migracion/       migración de datos desde la base de 2016
├─ openapi/                    documento OpenAPI generado en cada compilación
├─ infra/                      Bicep para los recursos de Azure
└─ docs/                       esta documentación
```

## Requisitos

- .NET SDK 10
- Node.js 24 LTS con npm
- Docker Desktop o un motor compatible, para SQL Server local y las pruebas de integración

## Arranque local

```bash
# 1. SQL Server en contenedor (la contraseña de sa está en compose.yaml, solo para desarrollo)
docker compose up -d sql

# 2. Secretos locales: nunca en appsettings
dotnet user-secrets set "ConnectionStrings:Tiie" "Server=127.0.0.1,1433;Database=Tiie;User Id=sa;Password=<contraseña de compose.yaml>;TrustServerCertificate=true" --project src/Tiie.Web
dotnet user-secrets set "Semilla:ContrasenaAdministrador" "<contraseña inicial del administrador>" --project src/Tiie.Web

# 3. Esquema de la base de datos
dotnet tool restore
dotnet ef database update --project src/Tiie.Infraestructura --startup-project src/Tiie.Web

# 4. Backend en https://localhost:7180
#    En Development siembra catálogos de ejemplo y el usuario «admin» (correo de Semilla:CorreoAdministrador).
#    Al entrar por primera vez pide configurar MFA con una app autenticadora.
dotnet run --project src/Tiie.Web

# 5. Frontend con recarga en caliente en http://localhost:5173
#    Vite reenvía /api y /mi-cita/{token} al backend.
cd src/Tiie.Web.Client
npm ci
npm run dev
```

En desarrollo, el captcha usa las claves de prueba de Cloudflare Turnstile y los correos se escriben como archivos `.eml` en `artifacts/correos/` en lugar de enviarse.

El contenedor de SQL Server solo escucha en `127.0.0.1`; usa esa dirección en la cadena de conexión, porque `localhost` puede resolverse primero a IPv6 y la conexión se agota.

### Notas para Windows

- **Testcontainers:** si las pruebas de integración fallan al iniciar el contenedor `testcontainers/ryuk` en Docker Desktop, define `TESTCONTAINERS_RYUK_DISABLED=true` en tu terminal. Las pruebas detienen su contenedor de SQL Server al terminar; en CI (Linux) Ryuk funciona normalmente.
- **npm:** los scripts usan `&&`. Si tu `npm config get script-shell` apunta a Windows PowerShell 5.1, ejecútalos con `--script-shell=cmd.exe` o cambia esa opción en tu configuración de usuario.

## Comandos frecuentes

| Tarea | Comando |
| --- | --- |
| Compilar todo | `dotnet build` |
| Pruebas de backend | `dotnet test` |
| Pruebas rápidas, sin contenedores | `dotnet test --filter-not-trait "Category=Integracion"` |
| Pruebas del frontend | `npm test` en `src/Tiie.Web.Client` |
| Lint y tipos del frontend | `npm run lint && npm run typecheck` |
| Tamaño del JavaScript inicial | `npm run tamano` en `src/Tiie.Web.Client`, después de `npm run build` |
| Regenerar tipos de la API | `npm run api:tipos` (después de `dotnet build`) |
| Nueva migración | `dotnet ef migrations add <Nombre> --project src/Tiie.Infraestructura --startup-project src/Tiie.Web` |
| Pruebas de extremo a extremo | `npx playwright test` en `tests/e2e` |
| Publicación | `npm run build` en el cliente y `dotnet publish src/Tiie.Web -c Release` |

## Documentación

Empieza por [docs/README.md](docs/README.md). Las reglas para colaborar, también para agentes de IA, están en [CLAUDE.md](CLAUDE.md) y [CONTRIBUTING.md](CONTRIBUTING.md). Para reportar una vulnerabilidad, sigue [SECURITY.md](SECURITY.md).
