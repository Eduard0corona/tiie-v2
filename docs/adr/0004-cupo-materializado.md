# 0004 · Cupo materializado con actualización condicional

- **Estado:** Propuesto
- **Fecha:** 2026-10-22

## Contexto

El procedimiento `CitaInsertar` del sistema anterior cuenta las citas de un horario y luego inserta, sin bloqueo. Azure SQL usa READ COMMITTED SNAPSHOT por defecto, así que dos reservas simultáneas pueden ver el mismo lugar libre y ocuparlo ambas. La apertura de temporada concentra muchas reservas sobre pocos horarios.

## Decisión

Guardar los lugares como filas: una tabla `Cupo` con una fila por oferta, fecha y hora, con `Capacidad` y `Ocupados`. Reservar ejecuta, dentro de la transacción de la cita:

```sql
UPDATE Cupo SET Ocupados = Ocupados + 1
WHERE CupoId = @CupoId AND Cerrado = 0 AND Ocupados < Capacidad;
```

Si afecta 0 filas, no hay lugar. Una restricción `CHECK (Ocupados BETWEEN 0 AND Capacidad)` protege contra errores de código. Los cupos se generan desde las plantillas por un proceso diario y bajo demanda.

## Consecuencias

- La sentencia es atómica: no hace falta bloquear tablas, subir el aislamiento ni reintentar.
- La disponibilidad es una consulta agrupada sobre una tabla indexada, rápida para toda la ventana.
- Aparece un proceso que mantener: la generación de cupos, que debe ser idempotente y no tocar cupos con citas.
- Cambiar una plantilla no afecta de inmediato los cupos con citas; el resumen de impacto lo hace visible.

## Alternativas consideradas

- **Contar y bloquear con `UPDLOCK, HOLDLOCK` o aislamiento SERIALIZABLE:** funciona, pero bloquea rangos de índice y es fácil de romper en un cambio posterior.
- **`sp_getapplock` por horario y fecha:** serializa bien, pero el candado vive fuera de los datos y depende de que todo el código lo use.
- **Contar sin bloqueo y corregir después:** acepta sobreventa, justo lo que hay que eliminar.
