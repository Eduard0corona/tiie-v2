# 06 · API

La API es JSON sobre HTTPS, del mismo origen que la SPA. Tiene tres grupos con distinto acceso: `/api/publico`, `/api/mi-cita` y `/api/admin`. La fuente de verdad del contrato es el documento OpenAPI que se genera al compilar (`openapi/Tiie.Web.json`); este archivo explica su uso y sus reglas.

## Convenciones

| Tema | Regla |
| --- | --- |
| Formato | JSON en camelCase; enums como texto (`"estado": "Agendada"`) |
| Fechas | `"2026-11-12"` (`DateOnly`, hora local de Sinaloa) |
| Horas | `"10:30"` (`TimeOnly`, hora local) |
| Instantes | ISO 8601 en UTC: `"2026-11-03T17:04:11Z"` |
| Paginación | `?pagina=1&tamano=50` (máximo 200). Respuesta `{ "elementos": [], "total": 0, "pagina": 1, "tamano": 50 }` |
| Protección CSRF | Todo `POST`, `PUT`, `PATCH` y `DELETE` envía `X-Tiie-Csrf: 1`; sin ella, 400 ([07](07-autenticacion-y-autorizacion.md#protección-csrf)) |
| Idempotencia | `POST /api/publico/citas` y `POST /api/admin/citas` exigen `Idempotency-Key` (GUID). Repetirla devuelve la misma cita |
| Concurrencia | Los recursos editables incluyen `version` (texto Base64 de `RowVersion`). `PUT` y `PATCH` la exigen; si cambió, 409 `Conflicto` |
| Versionado | Sin versión en la ruta: la SPA y la API se publican juntas. Un cambio incompatible se hace en un solo despliegue |
| Tipos en TypeScript | Se generan con `npm run api:tipos`; no se escriben a mano |

## Errores

Todas las respuestas de error son Problem Details (RFC 9457) con la extensión `codigo`, que la SPA usa para decidir el mensaje:

```json
{
  "title": "No hay lugar en el horario elegido",
  "status": 409,
  "codigo": "SinCupo",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

Los errores de validación agregan `errors` por campo:

```json
{
  "title": "Hay datos por corregir",
  "status": 422,
  "codigo": "Validacion",
  "errors": { "curp": ["La CURP no es válida."], "celular": ["Escribe 10 dígitos."] }
}
```

El documento OpenAPI declara este formato como el esquema `ProblemaApi` (con `codigo` como la enumeración `CodigoError`) y lo asigna como respuesta `default` de cada operación; la SPA toma de ahí sus tipos de error.

| HTTP | `codigo` | Cuándo |
| --- | --- | --- |
| 400 | `SolicitudInvalida` | JSON mal formado; falta `X-Tiie-Csrf` o `Idempotency-Key` |
| 401 | `NoAutenticado` | Sin sesión del personal o sin cookie de cita |
| 401 | `CredencialesInvalidas` | Usuario o contraseña incorrectos |
| 401 | `CodigoMfaInvalido` | Código del autenticador o de recuperación incorrecto |
| 403 | `Prohibido` | El rol o el módulo no tienen permiso; petición de otro origen |
| 403 | `MfaRequerido` | La sesión aún no configura MFA |
| 404 | `NoEncontrado` | El recurso no existe o no es visible para quien pregunta |
| 409 | `SinCupo` | El cupo se llenó |
| 409 | `YaTieneCita` | La CURP ya tiene una cita Agendada (RN-01) |
| 409 | `Conflicto` | `version` desactualizada, o se intenta borrar algo con historia |
| 422 | `Validacion` | Campos inválidos; ver `errors` |
| 422 | `CaptchaInvalido` | El captcha no pasó |
| 422 | `FueraDeVentana` | La fecha está fuera de la ventana (RN-03, RN-18) |
| 422 | `CupoCerrado` | El cupo está cerrado por un bloqueo (RN-07) |
| 422 | `OfertaInactiva` | La oferta, su módulo o su tipo están inactivos (RN-09) |
| 422 | `CitaNoModificable` | Fuera del plazo de cambios o la cita no está Agendada (RN-13) |
| 422 | `TokenInvalido` | Enlace de activación o de restablecimiento vencido |
| 423 | `CuentaBloqueada` | Demasiados intentos fallidos; incluye cuándo se desbloquea |
| 429 | `DemasiadasSolicitudes` | Límite de solicitudes; incluye `Retry-After` |
| 500 | `ErrorInterno` | Error inesperado; solo `traceId`, nunca detalles |
| 503 | `CorreoNoDisponible` | No se pudo enviar una invitación al personal. Al crear un usuario, incluye `usuarioId` |

## Público

Anónimo. Límites por IP ([11](11-seguridad-y-privacidad.md#límites-de-solicitudes)).

### `GET /api/publico/configuracion`

Valores que la SPA necesita al arrancar.

```json
{ "claveSitioCaptcha": "1x00000000000000000000AA", "versionAvisoPrivacidad": "2026-01", "zonaHoraria": "America/Mazatlan" }
```

### `GET /api/publico/tipos-tramite`

Tipos activos que se ofrecen en al menos un módulo activo, con requisitos.

```json
[
  {
    "tipoTramiteId": 1,
    "nombre": "Primera vez",
    "descripcion": "Estudiantes que nunca han tenido TIIE.",
    "costo": 0,
    "requisitos": [
      { "nivelEscolar": "Medio superior y superior", "texto": "Ficha de inscripción o comprobante de estudios del ciclo vigente." },
      { "nivelEscolar": null, "texto": "CURP." }
    ]
  }
]
```

### `GET /api/publico/municipios`

Municipios con al menos un módulo activo: `[{ "municipioId": 6, "nombre": "Culiacán" }]`.

### `GET /api/publico/municipios/{municipioId}/modulos`

```json
[
  {
    "moduloId": 3,
    "nombre": "Módulo Culiacán Centro",
    "direccion": "Av. Ejemplo 123, Col. Centro, C.P. 80000",
    "horarioAtencion": "Lunes a viernes de 8:00 a 15:00, sábado de 9:00 a 12:30",
    "latitud": 24.80,
    "longitud": -107.39
  }
]
```

### `GET /api/publico/modulos`

Todos los módulos activos con su municipio, para el directorio de sedes del inicio: los mismos campos que el anterior más `municipioId` y `municipio`.

### `GET /api/publico/modulos/{moduloId}/ofertas`

`[{ "ofertaId": 12, "tipoTramiteId": 1, "nombre": "Primera vez", "costo": 0, "notasSede": null }]`

### `GET /api/publico/ofertas/{ofertaId}/disponibilidad?desde=&hasta=`

`desde` y `hasta` son opcionales; por omisión, la ventana completa. El rango se recorta a la ventana (RN-03).

```json
{
  "ofertaId": 12,
  "desde": "2026-11-04",
  "hasta": "2026-12-22",
  "dias": [
    { "fecha": "2026-11-04", "estado": "Alta", "libres": 34, "capacidad": 40, "motivo": null },
    { "fecha": "2026-11-08", "estado": "SinServicio", "libres": 0, "capacidad": 0, "motivo": null },
    { "fecha": "2026-11-16", "estado": "Cerrado", "libres": 0, "capacidad": 0, "motivo": "Día inhábil" }
  ]
}
```

Estados: `Alta`, `Poca`, `Llena`, `Cerrado`, `SinServicio` ([03](03-reglas-de-negocio.md#disponibilidad-por-día)).

### `GET /api/publico/ofertas/{ofertaId}/cupos?fecha=`

Horas abiertas con lugar en esa fecha: `[{ "cupoId": 81234, "hora": "09:00", "libres": 3 }]`. Una fecha fuera de la ventana responde 422 `FueraDeVentana`.

### `POST /api/publico/citas`

Cabeceras: `X-Tiie-Csrf: 1` e `Idempotency-Key: <GUID>`. La CURP del ejemplo es ficticia.

```json
{
  "cupoId": 81234,
  "curp": "GOMA080515MSLRRNA5",
  "nombre": "Ana",
  "apellidoPaterno": "Gómez",
  "apellidoMaterno": "Martínez",
  "celular": "6671234567",
  "correo": "ana@example.com",
  "aceptaAvisoPrivacidad": true,
  "tokenCaptcha": "0.ZxYw…"
}
```

**201 Created**, con `Location: /api/mi-cita` y `Set-Cookie: tiie_cita=…; Path=/api/mi-cita; Secure; HttpOnly; SameSite=Lax`:

```json
{
  "folio": "261103-1000042",
  "estado": "Agendada",
  "fecha": "2026-11-12",
  "hora": "10:30",
  "modulo": { "moduloId": 3, "nombre": "Módulo Culiacán Centro", "direccion": "Av. Ejemplo 123, Col. Centro, C.P. 80000" },
  "tramite": { "tipoTramiteId": 1, "nombre": "Primera vez" }
}
```

Errores: 409 `SinCupo` o `YaTieneCita`; 422 `Validacion`, `CaptchaInvalido`, `FueraDeVentana`, `CupoCerrado` u `OfertaInactiva`; 429.

### `POST /api/publico/citas/recuperacion`

`{ "curp": "…", "correo": "…", "tokenCaptcha": "…" }` → **202** sin cuerpo, siempre, exista o no la cita (RF-06).

## Mi cita

Requiere la cookie `tiie_cita` ([07](07-autenticacion-y-autorizacion.md#acceso-del-ciudadano)).

| Método | Ruta | Cuerpo | Respuesta |
| --- | --- | --- | --- |
| GET | `/mi-cita/{token}` | — | 302 a `/mi-cita` con la cookie, o a `/mi-cita?error=enlace-invalido` |
| GET | `/api/mi-cita` | — | Detalle de la cita (abajo) |
| GET | `/api/mi-cita/comprobante.pdf` | — | `application/pdf` |
| POST | `/api/mi-cita/cancelacion` | `{ "motivo": "opcional" }` | 200 con el detalle actualizado; 422 `CitaNoModificable` |
| POST | `/api/mi-cita/reprogramacion` | `{ "cupoId": 81301 }` | 200 con el detalle de la cita nueva y la cookie actualizada; 409 `SinCupo`; 422 `CitaNoModificable` o `FueraDeVentana` |
| POST | `/api/mi-cita/salida` | — | 204; borra la cookie |

`GET /api/mi-cita`:

```json
{
  "folio": "261103-1000042",
  "folioAnterior": null,
  "estado": "Agendada",
  "fecha": "2026-11-12",
  "hora": "10:30",
  "ofertaId": 12,
  "modulo": {
    "nombre": "Módulo Culiacán Centro",
    "direccion": "Av. Ejemplo 123, Col. Centro, C.P. 80000",
    "municipio": "Culiacán",
    "horarioAtencion": "Lunes a viernes de 8:00 a 15:00",
    "latitud": 24.80,
    "longitud": -107.39
  },
  "tramite": {
    "nombre": "Primera vez",
    "costo": 0,
    "notas": "Presentarse 10 minutos antes.",
    "notasSede": null,
    "requisitos": [{ "nivelEscolar": null, "texto": "CURP." }]
  },
  "solicitante": { "nombreCompleto": "Ana Gómez Martínez", "curp": "GOMA************A5", "correo": "a***@example.com" },
  "puedeModificar": true,
  "modificableHasta": "2026-11-11"
}
```

Para reprogramar, la SPA usa la disponibilidad y los cupos públicos de `ofertaId`.

## Administración

Requiere la cookie `tiie_sesion`. Políticas ([07](07-autenticacion-y-autorizacion.md#roles-y-políticas)):

- **Personal:** cualquier rol con MFA configurada.
- **Supervisor:** Administrador o Supervisor.
- **Administrador:** solo Administrador.
- **+ módulo:** además, el módulo del recurso debe estar asignado al usuario (los administradores tienen todos).

Las listas se filtran a los módulos del usuario; un recurso de otro módulo responde 404, no 403, para no revelar que existe.

### Sesión y cuenta

| Método | Ruta | Acceso | Cuerpo | Respuesta |
| --- | --- | --- | --- | --- |
| POST | `/api/admin/auth/entrar` | Anónimo | `{ usuario, contrasena }` | 200 `{ siguientePaso: "Listo" \| "CodigoMfa" \| "ConfigurarMfa" }`; 401; 423 |
| POST | `/api/admin/auth/entrar/mfa` | Cookie temporal de 2FA | `{ codigo, recordarEsteEquipo }` | 204; 401 `CodigoMfaInvalido` |
| POST | `/api/admin/auth/entrar/recuperacion` | Cookie temporal de 2FA | `{ codigoRecuperacion }` | 204; 401 |
| POST | `/api/admin/auth/salir` | Sesión | — | 204 |
| GET | `/api/admin/auth/yo` | Sesión, aun sin MFA | — | `{ usuarioId, usuario, nombreCompleto, rol, modulos: [{ moduloId, nombre }], mfaActivo }` |
| GET | `/api/admin/auth/mfa` | Sesión sin MFA | — | `{ claveCompartida, uriAutenticador }` |
| POST | `/api/admin/auth/mfa` | Sesión sin MFA | `{ codigo }` | 200 `{ codigosRecuperacion: ["…"] }` (10 códigos); 422 `Validacion` en `errors.codigo` si el código no es correcto |
| POST | `/api/admin/auth/contrasena` | Personal | `{ actual, nueva }` | 204 |
| POST | `/api/admin/auth/activacion` | Anónimo | `{ usuarioId, token, contrasena }` | 204; 422 `TokenInvalido` |
| POST | `/api/admin/auth/olvide-contrasena` | Anónimo | `{ correo }` | 202 siempre, aunque falle el envío (se registra una advertencia) |
| POST | `/api/admin/auth/restablecimiento` | Anónimo | `{ usuarioId, token, contrasena }` | 204; 422 `TokenInvalido` |

### Agenda y citas

| Método | Ruta | Acceso | Cuerpo o parámetros | Respuesta |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/agenda` | Personal + módulo | `moduloId`, `fecha` | `[{ citaId, folio, hora, tramite, nombreCompleto, curp, estado, origen }]` |
| GET | `/api/admin/citas` | Personal | `desde`, `hasta`, `moduloId`, `tipoTramiteId`, `estado`, `busqueda` (folio, folio anterior o CURP), paginación | Página de citas |
| GET | `/api/admin/citas/{citaId}` | Personal + módulo | — | Detalle con historial de estados y notificaciones |
| POST | `/api/admin/citas` | Personal + módulo del cupo | `{ cupoId, curp, nombre, apellidoPaterno, apellidoMaterno, celular?, correo?, avisoLeido }` + `Idempotency-Key` | 201 `{ citaId, folio }` |
| POST | `/api/admin/citas/{citaId}/asistencia` | Personal + módulo | `{ asistio }` | 200 `{ estado }` |
| POST | `/api/admin/citas/{citaId}/correccion-asistencia` | Supervisor + módulo | `{ asistio, motivo }` | 200 `{ estado }` |
| POST | `/api/admin/citas/{citaId}/cancelacion` | Personal + módulo | `{ motivo }` | 200 `{ estado }` |
| POST | `/api/admin/citas/{citaId}/reprogramacion` | Personal + módulo | `{ cupoId, motivo }` | 200 `{ citaId, folio }` |
| POST | `/api/admin/citas/{citaId}/reenvio` | Personal + módulo | — | 202; encola la confirmación otra vez |
| GET | `/api/admin/citas/{citaId}/comprobante.pdf` | Personal + módulo | — | PDF |
| GET | `/api/admin/ofertas/{ofertaId}/cupos` | Personal + módulo | `desde`, `hasta` (admite hoy) | `[{ cupoId, fecha, hora, capacidad, ocupados, cerrado }]` |

### Catálogos

| Método | Ruta | Acceso | Notas |
| --- | --- | --- | --- |
| GET | `/api/admin/catalogos` | Personal | `{ municipios, nivelesEscolares, roles }` |
| GET | `/api/admin/modulos` | Personal | Filtrado a sus módulos; parámetro `activo` |
| POST | `/api/admin/modulos` | Administrador | Datos del módulo; 201 |
| GET | `/api/admin/modulos/{moduloId}` | Personal + módulo | Incluye `version` |
| PUT | `/api/admin/modulos/{moduloId}` | Administrador | Exige `version` |
| POST | `/api/admin/modulos/{moduloId}/activacion` | Administrador | `{ activo }`; responde `{ citasFuturas }` para decidir qué hacer con ellas |
| GET | `/api/admin/tipos-tramite` | Personal | |
| POST | `/api/admin/tipos-tramite` | Administrador | `{ nombre, descripcion, costo, notas, requisitos: [{ nivelEscolarId, texto }] }` |
| GET | `/api/admin/tipos-tramite/{tipoTramiteId}` | Personal | Incluye `version` |
| PUT | `/api/admin/tipos-tramite/{tipoTramiteId}` | Administrador | Reemplaza la lista de requisitos completa; exige `version` |
| GET | `/api/admin/ofertas` | Personal | Parámetro `moduloId` |
| GET | `/api/admin/ofertas/{ofertaId}` | Personal + módulo | Incluye `version` |
| POST | `/api/admin/ofertas` | Supervisor + módulo | `{ moduloId, tipoTramiteId, diasVentana, notasSede }` |
| PUT | `/api/admin/ofertas/{ofertaId}` | Supervisor + módulo | `{ diasVentana, notasSede, activo, version }` |

### Horarios y bloqueos

| Método | Ruta | Acceso | Notas |
| --- | --- | --- | --- |
| GET | `/api/admin/ofertas/{ofertaId}/plantilla` | Personal + módulo | Semana completa |
| PUT | `/api/admin/ofertas/{ofertaId}/plantilla` | Supervisor + módulo | Reemplaza la semana y regenera cupos |
| PATCH | `/api/admin/cupos/{cupoId}` | Supervisor + módulo | `{ capacidad?, cerrado?, version }`; `capacidad` no puede quedar por debajo de `ocupados` |
| GET | `/api/admin/bloqueos` | Personal | `desde`, `hasta`, `moduloId` |
| POST | `/api/admin/bloqueos` | Administrador (general) o Supervisor + módulo | `{ desde, hasta, moduloId?, ofertaId?, motivo }` |
| POST | `/api/admin/bloqueos/{bloqueoId}/cancelacion-citas` | Igual que el bloqueo | `{ motivo }`; cancela las citas afectadas y avisa con `CierrePorBloqueo` |
| DELETE | `/api/admin/bloqueos/{bloqueoId}` | Igual que el bloqueo | 204; reabre cupos |

Plantilla (`GET` y `PUT`):

```json
{
  "dias": [
    { "diaSemana": 1, "horas": [{ "hora": "08:00", "turnos": 4 }, { "hora": "08:30", "turnos": 4 }] },
    { "diaSemana": 6, "horas": [{ "hora": "09:00", "turnos": 3 }] }
  ]
}
```

Respuesta del `PUT`: `{ "cuposCreados": 120, "cuposActualizados": 36, "cuposEliminados": 12, "cuposConCitasSinCambio": 3 }`.

Respuesta de `POST /api/admin/bloqueos`:

```json
{
  "bloqueoId": 7,
  "cuposCerrados": 48,
  "citasAfectadas": [{ "citaId": 5012, "folio": "261103-1000042", "fecha": "2026-11-16", "hora": "10:30" }]
}
```

### Usuarios

Solo Administrador.

| Método | Ruta | Cuerpo | Notas |
| --- | --- | --- | --- |
| GET | `/api/admin/usuarios` | `busqueda`, `rol`, `activo`, paginación | |
| POST | `/api/admin/usuarios` | `{ usuario, correo, nombre, apellidoPaterno, apellidoMaterno, rol, moduloIds }` | 201 y envía la invitación. Si el correo falla, 503 `CorreoNoDisponible` con la extensión `usuarioId`: la cuenta quedó creada y la invitación se reenvía después |
| GET | `/api/admin/usuarios/{usuarioId}` | — | Incluye `version`, `mfaActivo`, `ultimoAccesoEn` |
| PUT | `/api/admin/usuarios/{usuarioId}` | `{ correo, nombre, apellidoPaterno, apellidoMaterno, rol, moduloIds, version }` | Cambia el sello de seguridad: la sesión del usuario se renueva en menos de 5 minutos |
| POST | `/api/admin/usuarios/{usuarioId}/activacion` | `{ activo }` | |
| POST | `/api/admin/usuarios/{usuarioId}/invitacion` | — | 202; reenvía la invitación |
| POST | `/api/admin/usuarios/{usuarioId}/mfa/restablecimiento` | — | 204; el usuario configura MFA de nuevo al entrar |

Un administrador no puede desactivarse a sí mismo ni quitarse el rol, y siempre debe quedar al menos un administrador activo (422 `Validacion`).

### Reportes, tablero y auditoría

| Método | Ruta | Acceso | Notas |
| --- | --- | --- | --- |
| GET | `/api/admin/reportes/citas` | Supervisor | `desde`, `hasta` (inclusivos), `moduloId`, `tipoTramiteId`, `estado`, `formato=json\|xlsx\|pdf` |
| GET | `/api/admin/tablero` | Personal | `moduloId` opcional; `{ hoy, proximos7Dias, inasistencia30Dias }` |
| GET | `/api/admin/auditoria` | Administrador | `desde`, `hasta`, `usuarioId`, `accion`, `entidad`, paginación |
| GET | `/api/admin/procesos` | Administrador | Última ejecución de cada proceso programado |

## Utilidades

| Método | Ruta | Notas |
| --- | --- | --- |
| GET | `/salud` | 200 si el sitio y la base de datos responden; sin datos sensibles |
| GET | `/openapi/v1.json` | Solo en Development |
