# 08 · Frontend

Una sola aplicación React con TypeScript, compilada por Vite en `src/Tiie.Web/wwwroot`. La parte pública se carga primero y es ligera; todo `/admin` se descarga solo cuando alguien entra al panel. La SPA no decide reglas de negocio: muestra lo que responde la API y traduce sus errores a mensajes.

## Stack

| Uso | Paquete |
| --- | --- |
| Interfaz | `react` y `react-dom` 19 |
| Rutas | `react-router` 7 (modo de datos con `createBrowserRouter`) |
| Estado del servidor | `@tanstack/react-query` 5 |
| Cliente HTTP tipado | `openapi-fetch`, con tipos de `openapi-typescript` |
| Formularios | `react-hook-form`, `zod` y `@hookform/resolvers` |
| Estilos | `tailwindcss` 4 con `@tailwindcss/vite` |
| Componentes base | Propios en `components/ui` (alerta, botón, campo, contenedor, diálogo, encabezado, grupo de opciones, ícono, insignia y selector), sin kit de componentes ni librería de íconos: los íconos son SVG propios en `Icono.tsx`. Tokens, componentes y pantallas del sitio público en [16](16-experiencia-de-usuario.md); los del panel (encabezado de página, paneles, tablas, estados) en [17](17-experiencia-del-panel.md#componentes-del-panel). Los colores de `Boton` no se sobrescriben con `className`: si hace falta otro aspecto, se agrega una variante |
| Calendario | `react-day-picker` 9, con un botón de día propio (número y punto de disponibilidad). Su hoja de estilos no tiene capa, así que los estilos del calendario en `styles/index.css` van fuera de `@layer` |
| Fechas | `date-fns` 4 y `@date-fns/tz` |
| Tablas del panel | HTML semántico con `caption` y encabezados de columna; sin librería |
| Gráficas del tablero | Medidores con CSS dentro de tablas accesibles; sin librería ([17](17-experiencia-del-panel.md#tablero)) |
| Captcha | Cloudflare Turnstile con un componente envoltorio (por ejemplo `@marsidev/react-turnstile`) |
| QR para MFA | `qrcode.react` 4, que dibuja el QR en el navegador |
| Compilación | `vite` 8 y `typescript` 5.9 (la versión que admiten `openapi-typescript` y `typescript-eslint`) |
| Pruebas | `vitest` 5 con `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom` y `msw` 3 |
| Calidad | `eslint` 10 con `typescript-eslint`, `eslint-plugin-react-hooks` y `eslint-plugin-jsx-a11y`; `prettier` |

Las versiones exactas están en `package.json`, sin `^`. `eslint-plugin-jsx-a11y` aún declara compatibilidad solo hasta ESLint 9; un `overrides` en `package.json` lo instala con ESLint 10, con el que funciona. Las tablas y el tablero no necesitaron librería: con HTML semántico son accesibles y el JavaScript inicial queda en unos 124 KB comprimidos. Si una pantalla llega a necesitar ordenar o paginar en el cliente, se evalúa `@tanstack/react-table` en su PR.

Una dependencia nueva se justifica en el PR. No se agregan jQuery, Moment.js, librerías de estado global ni kits de componentes completos.

## Estructura

```text
src/Tiie.Web.Client/
├─ index.html
├─ package.json
├─ vite.config.ts
├─ public/                       favicon, robots.txt
└─ src/
   ├─ main.tsx                   QueryClient y RouterProvider
   ├─ app/
   │  ├─ router.tsx              rutas; /admin se carga de forma perezosa
   │  ├─ LayoutPublico.tsx
   │  └─ LayoutAdmin.tsx
   ├─ api/
   │  ├─ esquema.d.ts            generado; no se edita
   │  ├─ cliente.ts              openapi-fetch, cabeceras y manejo de errores
   │  └─ errores.ts              ErrorApi y mensajes por código
   ├─ features/
   │  ├─ publico/
   │  │  ├─ inicio/
   │  │  ├─ agendar/             pasos Sede, Fecha, Hora y Datos
   │  │  ├─ mi-cita/
   │  │  ├─ recuperar/
   │  │  └─ aviso-privacidad/
   │  └─ admin/
   │     ├─ sesion/              entrar, código MFA, activar, restablecer, configurar MFA
   │     ├─ agenda/
   │     ├─ citas/
   │     ├─ modulos/
   │     ├─ tramites/
   │     ├─ ofertas/             plantilla semanal y cupos
   │     ├─ bloqueos/
   │     ├─ usuarios/
   │     ├─ reportes/
   │     ├─ tablero/
   │     └─ auditoria/
   ├─ components/
   │  ├─ ui/                     componentes base propios (docs/16#componentes)
   │  └─ …                       compartidos: CalendarioDisponibilidad, SelectorHora, Requisitos, ComoLlegar, Marca
   ├─ lib/
   │  ├─ fechas.ts               zona America/Mazatlan y formatos
   │  ├─ curp.ts                 validación de CURP, misma regla que el backend
   │  ├─ correo.ts               sugerencia para dominios de correo mal escritos
   │  └─ enmascarar.ts
   └─ styles/index.css
```

Cada carpeta de característica tiene sus páginas (`PaginaAgenda.tsx`), sus hooks de datos (`useAgenda.ts`), sus esquemas de Zod (`esquemas.ts`) y sus pruebas (`*.test.tsx`), juntos.

## Rutas

| Ruta | Pantalla | Acceso |
| --- | --- | --- |
| `/` | Inicio: trámites, requisitos y sedes | Público |
| `/agendar` | Asistente de reserva; el paso va en `?paso=sede\|fecha\|hora\|datos` | Público |
| `/mi-cita` | Detalle, comprobante, cancelar, reprogramar; `?nueva=1` muestra «Tu cita quedó agendada»; sin cookie, «Consulta tu cita» explica cómo entrar con el enlace del correo | Cookie de cita |
| `/recuperar` | Recuperar el enlace por correo | Público |
| `/aviso-de-privacidad` | Aviso integral | Público |
| `/admin/entrar` | Usuario, contraseña y código MFA | Anónimo |
| `/admin/activar` | Definir contraseña desde la invitación | Anónimo con token |
| `/admin/restablecer` | Nueva contraseña desde el correo | Anónimo con token |
| `/admin/configurar-mfa` | QR, primer código y códigos de recuperación | Sesión sin MFA |
| `/admin` | Redirige a `/admin/agenda` | Personal |
| `/admin/agenda` | Agenda del día y asistencia | Personal |
| `/admin/citas`, `/admin/citas/nueva`, `/admin/citas/:citaId` | Búsqueda, ventanilla y detalle | Personal |
| `/admin/modulos`, `/admin/modulos/:moduloId` | Módulos | Personal; edición solo Administrador |
| `/admin/tramites`, `/admin/tramites/:tipoTramiteId` | Tipos de trámite y requisitos | Personal; edición solo Administrador |
| `/admin/ofertas`, `/admin/ofertas/:ofertaId` | Oferta, plantilla semanal y cupos | Personal; edición Supervisor |
| `/admin/bloqueos` | Bloqueos y días inhábiles | Personal; edición Supervisor |
| `/admin/usuarios`, `/admin/usuarios/:usuarioId` | Usuarios | Administrador |
| `/admin/reportes` | Reportes y exportación | Supervisor |
| `/admin/tablero` | Indicadores | Personal |
| `/admin/auditoria` | Bitácora y procesos | Administrador |
| `/admin/mi-cuenta` | Cambiar contraseña | Personal |
| `*` | Página no encontrada | Público |

`/mi-cita/{token}` **no** es una ruta de React: la atiende el servidor ([07](07-autenticacion-y-autorizacion.md#acceso-del-ciudadano)). El router no debe declarar `/mi-cita/:token`.

Ocultar un menú según el rol es solo comodidad; la API decide el acceso. La búsqueda global del panel lleva a `/admin/citas?busqueda=…`, el mismo filtro de la pantalla Citas. La guarda de `/admin` consulta `GET /api/admin/auth/yo`: con 401 redirige a `/admin/entrar?volver=<ruta>`, y con `mfaActivo = false` a `/admin/configurar-mfa`.

## Cliente de la API

Los tipos salen del documento OpenAPI que genera el backend al compilar. Después de cambiar un endpoint: `dotnet build` y `npm run api:tipos`. CI falla si `src/api/esquema.d.ts` no coincide con el backend.

```ts
// src/api/cliente.ts
import createClient, { type Middleware } from 'openapi-fetch';
import type { paths } from './esquema';
import { ErrorApi } from './errores';

const convertirErrores: Middleware = {
  async onResponse({ response }) {
    if (response.ok) return response;
    const esJson = response.headers.get('content-type')?.includes('json');
    throw new ErrorApi(response.status, esJson ? await response.clone().json() : null);
  },
};

export const api = createClient<paths>({
  baseUrl: '/',
  credentials: 'same-origin',
  headers: { 'X-Tiie-Csrf': '1' }, // protección CSRF, ver 07
});
api.use(convertirErrores);
```

`ErrorApi` expone `status`, `codigo` y `errors`. Los formularios copian `errors` a los campos con `setError`.

## Estado del servidor

| Datos | Clave de consulta | `staleTime` |
| --- | --- | --- |
| Configuración, tipos de trámite, municipios, módulos, ofertas | `['publico', recurso, ...ids]` | 5 min |
| Disponibilidad | `['publico', 'disponibilidad', ofertaId]` | 30 s |
| Cupos de un día | `['publico', 'cupos', ofertaId, fecha]` | 0 (siempre se vuelve a pedir) |
| Mi cita | `['mi-cita']` | 0 |
| Sesión del personal | `['admin', 'sesion']` | 5 min |
| Recursos del panel | `['admin', recurso, filtros]` | 30 s |

Toda mutación invalida las claves que afecta: reservar invalida disponibilidad y cupos de esa oferta; guardar una plantilla invalida cupos y disponibilidad. Un 401 en una consulta de `['admin', …]` borra la caché del panel y redirige a entrar.

## Asistente de reserva

- La selección (municipio, módulo, oferta, fecha, cupo y paso) vive en la URL: el botón Atrás funciona y un enlace compartido lleva al mismo paso.
- Los datos personales viven solo en el estado del formulario. **No** se guardan en `localStorage`, `sessionStorage` ni en la URL.
- El formulario valida con Zod: CURP con `lib/curp.ts`, celular de 10 dígitos, correo, aceptación del aviso. Es solo para retroalimentación; la API valida de nuevo.
- Cada envío lleva una `Idempotency-Key` generada al abrir el paso de datos con `crypto.randomUUID()`. Un reintento por red lenta reutiliza la misma.
- Al crear la cita, navega a `/mi-cita?nueva=1`. La cookie de cita ya viene en la respuesta.
- La portada abre el asistente con la sede elegida: `/agendar?municipio=6&modulo=3`.
- Los pasos ya hechos del indicador de progreso son botones que cambian `paso` sin borrar lo elegido.
- El paso Fecha ofrece los tres primeros días con lugar («Lo más pronto») además del calendario; el paso Hora agrupa por Mañana y Tarde.
- Al salir del campo de correo, `lib/correo.ts` sugiere el dominio común más parecido («gmial.com» → «gmail.com»). Solo sugiere: la persona decide.

| `codigo` | Mensaje | Acción |
| --- | --- | --- |
| `SinCupo` | «Ese horario se acaba de llenar. Elige otro.» | Volver al paso Hora y recargar cupos |
| `YaTieneCita` | «Esta CURP ya tiene una cita agendada.» | Ofrecer «Recibir mi enlace por correo» con la CURP ya escrita |
| `FueraDeVentana`, `CupoCerrado`, `OfertaInactiva` | «Esa fecha ya no está disponible.» | Volver al paso Fecha y recargar disponibilidad |
| `CaptchaInvalido` | «Confirma que no eres un robot.» | Reiniciar el captcha |
| `Validacion` | Mensajes por campo | Marcar los campos |
| `DemasiadasSolicitudes` | «Demasiados intentos. Espera unos minutos.» | Deshabilitar el botón según `Retry-After` |
| Otros | «Algo salió mal. Intenta de nuevo.» | Mostrar el `traceId` en letra pequeña |

## Fechas y zona horaria

Las fechas de la API son texto `yyyy-MM-dd` en hora de Sinaloa. Se muestran sin convertirlas a `Date` en UTC.

```ts
// src/lib/fechas.ts
import { TZDate } from '@date-fns/tz';
import { format } from 'date-fns';
import { es } from 'date-fns/locale';

export const ZONA = 'America/Mazatlan';

/** Hoy en Sinaloa, como 'yyyy-MM-dd'. */
export function hoyLocal(): string {
  return format(TZDate.tz(ZONA), 'yyyy-MM-dd');
}

/** '2026-11-12' → 'jueves 12 de noviembre de 2026'. */
export function formatearFechaLarga(fechaIso: string): string {
  const [anio, mes, dia] = fechaIso.split('-').map(Number);
  return format(new TZDate(anio, mes - 1, dia, ZONA), "EEEE d 'de' MMMM 'de' yyyy", { locale: es });
}
```

Prohibido `new Date('2026-11-12')`: el navegador la interpreta como medianoche UTC, que en Sinaloa es el día anterior. Ese error es el origen del `.AddDays(1)` del sistema anterior. ESLint lo prohíbe con `no-restricted-syntax`.

## Accesibilidad

Meta: WCAG 2.1 AA (RNF-08).

- Cada campo tiene `<label>` visible; los errores se asocian con `aria-describedby` y se anuncian con una región `aria-live="polite"`.
- Al cambiar de paso, el foco va al encabezado del paso.
- El calendario se maneja con teclado y anuncia el estado de cada día («martes 12, poca disponibilidad»); el estado no depende solo del color.
- Contraste de 4.5:1 en texto; objetivos táctiles de 44 × 44 px o más.
- Se respeta `prefers-reduced-motion`.
- Playwright corre axe en cada pantalla pública; un hallazgo grave o crítico falla el CI ([12](12-pruebas.md)).
- Las decisiones de accesibilidad del diseño público (radios visibles, foco en las horas, etiqueta visible dentro del nombre accesible) están en [16](16-experiencia-de-usuario.md#accesibilidad).

## Rendimiento

- `/admin` se carga con la propiedad `lazy` de las rutas: el JavaScript del panel no llega al ciudadano.
- Presupuesto del sitio público: menos de 200 KB de JavaScript inicial comprimido y LCP menor a 2.5 s en 4G simulada (RNF-14). CI reporta el tamaño del paquete.
- Sin mapas incrustados: «Cómo llegar» abre `https://www.google.com/maps/search/?api=1&query={lat},{lng}` en una pestaña nueva. Así no hay API key ni scripts de terceros.
- Imágenes en WebP o AVIF con `width` y `height` declarados.

## Compilación e integración con ASP.NET Core

```ts
// vite.config.ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

const backend = { target: 'https://localhost:7180', secure: false };

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../Tiie.Web/wwwroot',
    emptyOutDir: true,
    sourcemap: 'hidden',
  },
  server: {
    port: 5173,
    proxy: {
      '/api': backend,
      '^/mi-cita/.+': backend, // el intercambio del token lo hace el servidor
    },
  },
});
```

```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "lint": "eslint .",
    "typecheck": "tsc -b",
    "test": "vitest run",
    "tamano": "node scripts/tamano-inicial.mjs",
    "api:tipos": "openapi-typescript ../../openapi/Tiie.Web.json -o src/api/esquema.d.ts"
  }
}
```

- `vitest.config.ts` usa `jsdom`, el `setup` de `src/test/` (MSW y jest-dom) y el alias `@` de `src`.
- El cliente usa `window.location.origin` como `baseUrl`: es el mismo origen y así también funciona en las pruebas.
- `npm run tamano` mide el JavaScript inicial ya compilado (el script de entrada y sus `modulepreload`) y falla por encima de 200 KB comprimido.
- Los scripts usan `&&`: en Windows, npm debe usar `cmd.exe` u otro intérprete compatible como `script-shell`; Windows PowerShell 5.1 no lo acepta.

- En desarrollo se trabaja en `http://localhost:5173`; Vite reenvía la API al backend. Los navegadores aceptan cookies `Secure` en `localhost`.
- `src/Tiie.Web/wwwroot` es un resultado de compilación: está en `.gitignore`.
- En CI, la SPA se compila antes de `dotnet publish`, para que `MapStaticAssets` incluya los archivos ([13](13-despliegue-y-operacion.md#cicd)).
- `index.html` se sirve con `Cache-Control: no-cache`; los archivos de `assets/` llevan huella en el nombre y se guardan en caché un año.

## Pruebas

| Qué | Herramienta | Ejemplos |
| --- | --- | --- |
| Funciones de `lib/` | Vitest | CURP con `tests/vectores-curp.json`, fechas en el cambio de día UTC |
| Componentes y flujos | Testing Library + MSW | Asistente completo con `SinCupo` y `YaTieneCita` simulados |
| Errores de la API | Vitest | Cada `codigo` produce su mensaje |
| Pantallas reales | Playwright en `tests/e2e` | Ver [12](12-pruebas.md) |

Los manejadores de MSW se escriben con los tipos de `esquema.d.ts`, así que una respuesta simulada con forma incorrecta no compila.
