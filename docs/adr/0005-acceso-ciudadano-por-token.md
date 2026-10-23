# 0005 · Acceso del ciudadano por token, sin cuenta

- **Estado:** Propuesto
- **Fecha:** 2026-10-22

## Contexto

En el sistema anterior, cualquiera que conozca una CURP abre el comprobante con nombre, celular y correo. La CURP se deduce de datos públicos y muchos titulares son menores de edad. Pedir una cuenta al ciudadano agregaría fricción a un trámite que se hace una vez al año.

## Decisión

- Al crear una cita se genera un token de 32 bytes aleatorios. Se guarda su hash SHA-256 para buscarlo y una copia protegida con Data Protection para incluirlo en los correos.
- El correo lleva `{UrlPublica}/mi-cita/{token}`. El servidor cambia el token por una cookie `tiie_cita` limitada a `/api/mi-cita` y redirige, para que el token no quede en la barra de direcciones.
- El token vence al final del día siguiente a la cita.
- La recuperación pide CURP y correo, y solo envía el enlace al correo registrado; la respuesta es igual exista o no la cita.
- Ningún endpoint devuelve datos de una cita a partir de una CURP.

## Consecuencias

- Solo quien tiene acceso al correo registrado ve la cita.
- Si el ciudadano pierde el acceso a su correo, necesita al personal del módulo, que puede consultar y reenviar.
- La copia protegida del token depende de las llaves de Data Protection: si se pierden, los enlaces ya enviados dejan de servir. Las llaves se guardan en Storage con bloqueo contra borrado.
- El enlace del correo funciona como una contraseña de un solo propósito; por eso la página que lo recibe no envía `Referer` ni se guarda en caché.

## Alternativas consideradas

- **Cuentas de ciudadano:** más fricción y más datos que proteger para un trámite esporádico.
- **CURP y fecha de nacimiento:** ambos datos se deducen o se obtienen con facilidad; no son un secreto.
- **Código por SMS:** costo por mensaje y dependencia de un proveedor más; queda como canal futuro.
- **Guardar solo el hash:** impediría enviar el mismo enlace en el recordatorio y en la recuperación sin invalidar el anterior.
