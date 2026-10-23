# 11 · Seguridad y privacidad

El sistema guarda datos personales de estudiantes, muchos menores de edad, y lo opera un sujeto obligado. La versión 2 se diseña para que nadie vea datos de otra persona, para que una cuenta robada del personal sirva de poco y para que las reglas no se puedan saltar desde el navegador. Meta: OWASP ASVS nivel 2.

## Amenazas y controles

| Amenaza | Controles |
| --- | --- |
| Consultar datos de otra persona con su CURP | No hay endpoints por CURP; acceso con token de 256 bits; recuperación con respuesta idéntica; datos enmascarados en Mi cita |
| Robo de la contraseña de alguien del personal | MFA obligatoria; bloqueo tras 5 intentos; contraseñas frecuentes rechazadas; auditoría de accesos |
| Un capturista accede a funciones o módulos ajenos | Política por grupo de rutas; verificación de módulo en cada caso de uso; prueba automática por endpoint |
| Bots acaparan los turnos al abrir la temporada | Turnstile; límites por IP y por CURP; una cita activa por CURP; alerta por reservas por minuto |
| Saltarse reglas enviando peticiones a mano | Reglas solo en el servidor; el cupo define oferta, módulo y fecha; restricciones `CHECK` e índices únicos en SQL |
| CSRF | Cookies `SameSite=Lax`, cabecera `X-Tiie-Csrf` obligatoria y Fetch Metadata ([07](07-autenticacion-y-autorizacion.md#protección-csrf)) |
| XSS | React escapa todo; prohibido `dangerouslySetInnerHTML`; CSP sin scripts en línea; requisitos y notas como texto plano |
| Inyección SQL | EF Core parametrizado; SQL a mano solo con `ExecuteSql` o `SqlQuery` interpolados, que también se parametrizan |
| Secretos expuestos | Key Vault, identidad administrada, escaneo de secretos en CI |
| Dependencias vulnerables | Dependabot o Renovate; `dotnet list package --vulnerable` y `npm audit --audit-level=high` en CI |
| Uso del sitio para mandar correos a terceros | La recuperación solo escribe al correo registrado; límites por CURP y por IP |
| Clickjacking | `frame-ancestors 'none'` |
| Pérdida de datos | Restauración a un punto en el tiempo de 35 días; retención de largo plazo; ensayo de restauración trimestral |

## Cabeceras

`UseCabecerasDeSeguridad` agrega a todas las respuestas:

| Cabecera | Valor |
| --- | --- |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` (vía `UseHsts`) |
| `Content-Security-Policy` | Ver abajo |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin`; `no-referrer` en `/mi-cita/*` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` |
| `Cross-Origin-Opener-Policy` | `same-origin` |

```text
default-src 'self';
script-src 'self' https://challenges.cloudflare.com;
frame-src https://challenges.cloudflare.com;
style-src 'self' 'unsafe-inline';
img-src 'self' data:;
font-src 'self';
connect-src 'self';
object-src 'none';
base-uri 'self';
form-action 'self';
frame-ancestors 'none';
upgrade-insecure-requests
```

`'unsafe-inline'` en estilos es necesario porque los componentes de Radix posicionan elementos con atributos `style`. No se permite en scripts.

## Límites de solicitudes

Con el middleware de límites de ASP.NET Core. La IP real se toma de `X-Forwarded-For` con `UseForwardedHeaders`, configurado para confiar solo en el proxy de App Service.

| Política | Rutas | Límite | Partición |
| --- | --- | --- | --- |
| `PublicoLectura` | `GET /api/publico/*` | 120 por minuto | IP |
| `Reserva` | `POST /api/publico/citas` | 10 cada 10 minutos | IP |
| `Recuperacion` | `POST /api/publico/citas/recuperacion` | 5 por hora por IP; 3 por hora por CURP | IP; la de CURP se aplica en el caso de uso |
| `EnlaceCita` | `GET /mi-cita/{token}` | 20 cada 10 minutos | IP |
| `MiCita` | `/api/mi-cita/*` | 60 por minuto | Cita |
| `Entrar` | `POST /api/admin/auth/entrar*`, `olvide-contrasena` | 10 por minuto | IP |
| `Admin` | `/api/admin/*` | 300 por minuto | Usuario |

Los valores se leen de la sección `Limites` de la configuración (por ejemplo `Limites:Reserva:Permitidas` y `Limites:Reserva:VentanaMinutos`), para ajustarlos sin desplegar. Al exceder un límite: 429 `DemasiadasSolicitudes` con `Retry-After`. Los contadores son por instancia; si se escala a varias instancias en temporada, se evalúa Front Door con WAF para limitar en el borde.

## Captcha

- Cloudflare Turnstile en la reserva y en la recuperación.
- El servidor verifica el token con `https://challenges.cloudflare.com/turnstile/v0/siteverify`, enviando la clave secreta y la IP. Un token se acepta una sola vez.
- Si Turnstile no responde en 3 s, la reserva se rechaza con `CaptchaInvalido` y se registra una advertencia; no se deja pasar sin verificar.

## Datos personales

| Dato | Dónde | Para qué | Quién lo ve |
| --- | --- | --- | --- |
| CURP | `Solicitante.Curp` | Identificar a la persona y aplicar RN-01 | El ciudadano (enmascarada); personal del módulo de la cita |
| Nombre y apellidos | `Solicitante` | Atender la cita y el comprobante | El ciudadano; personal del módulo |
| Fecha de nacimiento y sexo | `Solicitante`, derivados de la CURP | Estadística agregada | Solo reportes agregados |
| Celular y correo | `Solicitante`; destinatario en `Notificacion` | Avisos de la cita | El ciudadano (enmascarado); personal del módulo |
| IP | `Auditoria` (solo acciones del personal) | Seguridad | Administrador |

Reglas:

- **Datos mínimos.** No se piden documentos, domicilio ni datos del tutor en esta versión.
- **Enmascarado** en pantallas del ciudadano, logs y auditoría: `Enmascarar.Curp("GOMA080515MSLRRNA5")` → `GOMA************A5`; correo → `a***@example.com`; celular → `******4567`.
- **Logs sin datos personales.** Los logs nunca incluyen cuerpos de petición; una prueba revisa que los mensajes de log de los casos de uso no contengan los valores de una reserva de prueba.
- **Telemetría sin tokens ni CURP.** Application Insights recibe `/mi-cita/{token}` con el token reemplazado y las cadenas de consulta con los valores ocultos. Los registros HTTP de App Service (`AppServiceHTTPLogs`) no se envían a Log Analytics porque guardan la URL completa.
- **Cifrado.** En tránsito con TLS 1.2 o superior; en reposo con TDE de Azure SQL y cifrado de Storage.
- **Acceso del personal** limitado a sus módulos y registrado en auditoría.

### Retención y anonimización

- `Agenda:MesesRetencion` define cuántos meses después de su última cita se anonimiza un solicitante. Mientras no se decida, está vacío y el proceso no hace nada.
- Anonimizar: `Curp`, `Celular` y `Correo` a `NULL`; nombre y apellidos a «ANONIMIZADO»; `AnonimizadoEn = ahora`. Las citas se conservan para estadística.
- Las notificaciones enviadas se borran a los 180 días.
- La base del sistema anterior queda en solo lectura y luego se archiva según la misma política ([14](14-migracion-de-datos.md)).

### Aviso de privacidad y derechos ARCO

- El aviso integral y el simplificado los redacta el área jurídica. Deben indicar responsable, finalidades, transferencias, mecanismos para ejercer derechos ARCO y cómo se comunican los cambios.
- Cada cita guarda la versión del aviso aceptada (`VersionAvisoPrivacidad`), que viene de `Sitio:VersionAvisoPrivacidad`.
- Las solicitudes de acceso, rectificación, cancelación u oposición las atiende un administrador con un procedimiento documentado. La anonimización manual de un solicitante usa el mismo código que el proceso de retención.

## Secretos

| Secreto | Dónde vive |
| --- | --- |
| Acceso a Azure SQL | Sin secreto: identidad administrada asignada por el usuario (`Authentication=Active Directory Default`) |
| Clave secreta de Turnstile | Key Vault, como referencia en la configuración de App Service |
| Llaves de Data Protection | Storage, protegidas con una llave de Key Vault |
| Envío de correo | Sin secreto: identidad administrada hacia Communication Services, con las claves de acceso del recurso desactivadas |
| Despliegue | Sin secreto: credencial federada OIDC de GitHub Actions; publicación básica (FTP y SCM) desactivada |
| Desarrollo local | `dotnet user-secrets`; nunca en `appsettings.*.json` |

CI ejecuta un escáner de secretos (por ejemplo gitleaks) en cada PR. Si un secreto llega al repositorio: primero se rota, después se limpia el historial.

## Verificación

- Revisión contra OWASP ASVS nivel 2 al cierre de la Fase 3.
- Prueba de penetración externa en la Fase 4; ningún hallazgo alto o crítico abierto para salir a producción.
- Pruebas automáticas de autorización por endpoint y de cabeceras ([12](12-pruebas.md)).

## Incidentes

1. Contener: desactivar la cuenta, rotar el secreto o cerrar la ruta afectada.
2. Evaluar: alcance con la auditoría y Application Insights.
3. Notificar: al responsable de datos personales de la dependencia, según la ley aplicable.
4. Corregir y documentar la causa en el repositorio.

## Fase 0: sistema actual

Mientras se construye la versión 2, el sistema de 2016 sigue en producción con riesgos abiertos. Estas acciones no esperan:

- [ ] Rotar la contraseña de Azure SQL y la de la cuenta de Gmail; desactivar la cuenta de Gmail si ya no se usa.
- [ ] Restringir o rotar la API key de Google Maps.
- [ ] Exigir sesión en `json/jTramite.aspx` y verificar el rol en cada página de administración.
- [ ] Retirar del servidor el prototipo `TarjetaOnLine` si está publicado.
- [ ] Quitar el script cargado desde `preview.oklerthemes.com`.
- [ ] Poner `debug="false"` y `customErrors` en los `Web.config`.
- [ ] Obtener el código desplegado para confirmar qué difiere del repositorio.
