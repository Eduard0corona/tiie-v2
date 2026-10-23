# 0002 · React servido por el mismo sitio, con público y `/admin`

- **Estado:** Aceptado
- **Fecha:** 2026-10-22

## Contexto

Hoy hay dos sitios separados: uno público y uno de administración, con plantillas y librerías duplicadas. El análisis inicial proponía páginas generadas en el servidor (Razor Pages); el equipo decidió usar React en el frontend y un solo sitio para el ciudadano y el personal.

## Decisión

Una aplicación React con TypeScript, compilada por Vite en `wwwroot` de `Tiie.Web`. ASP.NET Core sirve los archivos estáticos, la API bajo `/api` y `index.html` como respaldo para cualquier otra ruta. La parte pública y `/admin` comparten la aplicación, pero el código de `/admin` se carga de forma perezosa.

## Consecuencias

- Mismo origen para la SPA y la API: cookies de primera parte, sin CORS y sin tokens en el navegador.
- Un solo despliegue mantiene sincronizados la SPA y la API; el contrato se protege generando los tipos de TypeScript desde OpenAPI.
- El ciudadano no descarga el código del panel.
- El contenido de la página de inicio depende de JavaScript para mostrarse. Si el posicionamiento en buscadores llega a importar, se agregan metadatos estáticos en `index.html` o se prerenderiza solo `/`.
- La protección CSRF se resuelve con cookies `SameSite`, una cabecera propia y Fetch Metadata ([07](../07-autenticacion-y-autorizacion.md#protección-csrf)).
- Hay que cuidar el tamaño del paquete público; CI lo mide.

## Alternativas consideradas

- **Razor Pages:** menos piezas y sin compilación de JavaScript, pero el equipo prefiere React para una interfaz interactiva como el asistente y el editor de plantillas.
- **Dos sitios (público y administración):** duplica despliegues, dominios y configuración, y repite el problema actual.
- **Next.js u otro framework con renderizado en servidor:** agrega un servidor Node.js al lado de ASP.NET Core sin una necesidad clara de SEO.
