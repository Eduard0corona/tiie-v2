# 0003 · ASP.NET Core Identity con cookies y MFA obligatoria

- **Estado:** Aceptado
- **Fecha:** 2026-10-22

## Contexto

El sistema anterior guarda contraseñas con SHA-512 y una clave fija expuesta en el repositorio, no limita intentos ni valida el rol en el servidor. El equipo decidió usar ASP.NET Core Identity. Las cuentas son solo del personal y las crea un administrador; el ciudadano no tiene cuenta.

## Decisión

- `AddIdentityCore<Usuario>` con almacenes de EF Core, `SignInManager`, roles y proveedores de tokens predeterminados.
- Autenticación por cookie (`tiie_sesion`), no por tokens bearer.
- MFA obligatoria con app autenticadora (TOTP) y códigos de recuperación. Una sesión sin MFA configurada solo puede configurarla.
- Contraseñas de 12 caracteres o más, sin reglas de composición, contra una lista de contraseñas frecuentes; PBKDF2 con 210 000 iteraciones; bloqueo tras 5 intentos.
- Alta por invitación: el administrador crea la cuenta y el usuario define su contraseña con un enlace de un solo uso.
- Endpoints propios de autenticación; no se usa `MapIdentityApi`.
- CSRF con `SameSite=Lax`, cabecera `X-Tiie-Csrf` y Fetch Metadata, en lugar de los tokens de `IAntiforgery`.

## Consecuencias

- Sin tokens en `localStorage`: un XSS no puede robar la sesión.
- El sello de seguridad, validado cada 5 minutos, cierra sesiones al desactivar usuarios o cambiar permisos.
- El equipo mantiene las pantallas de entrar, activar, restablecer y configurar MFA.
- Los tokens de `IAntiforgery` se atan a la identidad del esquema por defecto; con dos esquemas (personal y cita) producirían rechazos falsos. Por eso la protección CSRF usa otra técnica, documentada en [07](../07-autenticacion-y-autorizacion.md#protección-csrf).

## Alternativas consideradas

- **Microsoft Entra ID:** delega MFA y ciclo de vida de cuentas, pero se decidió Identity. Puede agregarse después como proveedor de inicio de sesión externo.
- **`MapIdentityApi`:** expone registro público y tokens bearer; aquí las cuentas solo las crea un administrador.
- **JWT guardado en el navegador:** expuesto a XSS y más complejo de revocar que una cookie con sello de seguridad.
