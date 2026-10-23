# 0007 · Bandeja de salida en SQL y procesos dentro del sitio

- **Estado:** Propuesto
- **Fecha:** 2026-10-22

## Contexto

En el sistema anterior el correo se envía dentro de la petición de reserva: si el proveedor falla, la cita queda creada pero el ciudadano ve un error, reintenta y se topa con «ya tiene cita». Además hay tareas diarias (generar cupos, cerrar citas pasadas, recordatorios) que necesitan correr aunque nadie use el sitio. El análisis inicial proponía un *worker* desplegado aparte; con un solo sitio se busca la opción con menos piezas.

## Decisión

- Las notificaciones se insertan en la tabla `Notificacion` en la misma transacción que la cita (patrón *outbox*). Un proceso las envía con reintentos y retrasos crecientes.
- Los procesos corren como `BackgroundService` dentro de `Tiie.Web`. Con varias instancias:
  - el envío reserva lotes con `UPDATE … WITH (READPAST, UPDLOCK)` y un tiempo de reserva;
  - los procesos diarios toman `sp_getapplock` por nombre y registran su ejecución en `EjecucionProceso`, lo que también permite ponerse al día después de un reinicio.
- Este ADR reemplaza la propuesta de *worker* separado del documento de análisis.

## Consecuencias

- Una reserva nunca falla por el correo, y un correo nunca se pierde por una caída: queda pendiente.
- Un solo despliegue; sin colas ni servicios adicionales.
- Los procesos comparten CPU y memoria con las peticiones web; si crecen, se mueven a un despliegue aparte sin cambiar su código, porque viven en `Tiie.Aplicacion`.
- *Always On* debe estar activo en App Service para que los procesos corran sin tráfico.

## Alternativas consideradas

- **Worker en un App Service aparte:** más despliegues y configuración; App Service espera que una aplicación responda HTTP.
- **Azure Functions con temporizador:** otro runtime, otro despliegue y otra identidad que administrar.
- **Hangfire o Quartz:** útiles, pero agregan tablas, panel y dependencias para cinco procesos simples.
- **Azure Service Bus:** robusto, pero excesivo para el volumen esperado de correos.
