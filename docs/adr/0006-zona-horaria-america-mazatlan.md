# 0006 · Zona `America/Mazatlan` y `TimeProvider`

- **Estado:** Propuesto
- **Fecha:** 2026-10-22

## Contexto

Azure SQL trabaja en UTC y Sinaloa está en UTC-7. El sistema anterior usa `GETDATE()` para decidir «hoy», así que a partir de las 17:00 hora local el calendario se recorre un día y la regla de una cita vigente falla. En el navegador, convertir `"2026-11-12"` con `new Date()` da el día anterior; el sistema anterior lo compensaba sumando un día.

## Decisión

- La zona de negocio es `America/Mazatlan`, configurable en `Agenda:ZonaHoraria`.
- Todo cálculo de «hoy» o «ahora» pasa por `RelojAgenda`, que usa `TimeProvider`. Está prohibido `DateTime.Now`, `DateTime.UtcNow` y `GETDATE()` en reglas.
- Fechas de cita como `DateOnly` (`date`), horas como `TimeOnly` (`time(0)`), instantes como `DateTimeOffset` en UTC (`datetimeoffset(0)`).
- La API envía fechas y horas como texto (`"2026-11-12"`, `"10:30"`). El frontend las formatea con `@date-fns/tz` sin pasar por UTC.

## Consecuencias

- Las reglas dependen de un reloj inyectable: las pruebas fijan cualquier instante con `FakeTimeProvider`, incluido el cambio de día UTC.
- El servidor necesita los datos de zonas horarias del sistema (`InvariantGlobalization = false`).
- Si México vuelve a cambiar sus reglas de horario de verano, basta con actualizar los datos de zonas horarias del sistema operativo y del runtime.

## Alternativas consideradas

- **Usar la hora local del servidor:** en Azure es UTC y cambia según dónde se ejecute.
- **Todo en UTC, incluidas las fechas de cita:** una cita es un día y una hora locales; guardarla en UTC complica cada consulta por día.
- **Desplazamiento fijo de -7 horas:** se rompe si cambian las reglas de horario de la zona.
