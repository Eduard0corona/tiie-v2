# Pruebas extremo a extremo

Playwright con axe en un perfil de celular (Pixel 7) y uno de escritorio. Cubren los nueve flujos de [docs/12](../../docs/12-pruebas.md#extremo-a-extremo) y revisan la accesibilidad de cada pantalla pública.

| Archivo | Flujos |
| --- | --- |
| `pruebas/personal.setup.ts` | Una sola entrada al panel para las pruebas del personal |
| `pruebas/publico.spec.ts` | 1 reserva completa, 2 horario lleno a mitad del flujo, 3 CURP con cita activa, y axe en las pantallas sin cita |
| `pruebas/mi-cita.spec.ts` | 4 enlace del correo, comprobante y cancelación; 5 reprogramación |
| `pruebas/personal.spec.ts` | 6 entrar con MFA y marcar asistencia; 7 plantilla y disponibilidad pública; 8 bloqueo con cancelación; 9 alta de usuario con activación y MFA |

Las pruebas crean sus propios datos (CURP válidas y nuevas, citas por la API) y no dependen de citas existentes. Corren una a la vez porque comparten plantillas, cupos y bloqueos; las que cambian algo lo restauran al terminar.

## Variables

| Variable | Uso |
| --- | --- |
| `E2E_URL` | Sitio a probar; por omisión `https://localhost:7180` |
| `E2E_USUARIO`, `E2E_CONTRASENA` | Un Administrador del panel. Sin ellas, las pruebas del personal se omiten |
| `E2E_TOTP` | Clave de su app autenticadora. En local no hace falta: si la cuenta aún no tiene MFA, la preparación lo configura y guarda la clave en `.auth/` (fuera de git) |
| `E2E_CARPETA_CORREOS` | Buzón de prueba: la carpeta del proveedor de correo `Archivo`. Por omisión `artifacts/correos` en la raíz del repositorio. Si no existe, se omiten los flujos 4 y 9 |
| `E2E_CANAL` | `msedge` usa el Edge instalado en lugar de descargar Chromium |

## En local

1. Levanta el sitio en Development (`https://localhost:7180`, correo `Archivo`). Sube los límites de solicitudes: la suite reserva y entra muchas veces desde la misma IP, por ejemplo con `Limites__Reserva__Permitidas=10000` y lo mismo para `Recuperacion`, `EnlaceCita` y `Entrar`.
2. Instala y corre:

   ```bash
   npm ci
   E2E_USUARIO=admin E2E_CONTRASENA="<contraseña del administrador semilla>" E2E_CANAL=msedge npx playwright test
   ```

El flujo 6 necesita una cita de hoy, y `GeneracionCupos` crea cupos desde mañana: en una base recién creada no hay cupos de hoy y la prueba se omite con ese motivo.

## En QA

El job `e2e` de `.github/workflows/despliegue.yml` corre después de cada despliegue a QA, con `E2E_USUARIO`, `E2E_CONTRASENA` y `E2E_TOTP` de un Administrador de pruebas. QA necesita:

- Las claves de prueba de Turnstile (`Captcha__ClaveSitio` = `1x00000000000000000000AA` y la secreta de prueba en Key Vault): la API de preparación reserva con el token de prueba y un navegador automatizado no pasa un captcha real.
- Límites de solicitudes que admitan unas 40 reservas y 15 entradas por corrida desde la IP del runner.

Sin buzón de prueba en QA, los flujos 4 y 9 se omiten.

## Dependencias

| Paquete | Por qué |
| --- | --- |
| `@playwright/test` | Ejecutor y navegador automatizado de las pruebas E2E (docs/12) |
| `@axe-core/playwright` | Revisión de accesibilidad WCAG 2.1 AA en cada pantalla pública (docs/12) |
| `playwright-core` | Fija la versión que usa `@axe-core/playwright` a la de `@playwright/test`, para que los tipos coincidan |
| `typescript`, `@types/node` | Verificación de tipos (`npm run typecheck`), con las mismas versiones que la SPA |
