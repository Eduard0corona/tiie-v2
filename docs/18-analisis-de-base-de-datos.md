# 18 · Análisis de la base de datos

Revisión de la estructura, los índices, las consultas y los scripts de la base, hecha el 23 de octubre de 2026 antes del primer despliegue. Se midió el SQL que de verdad genera EF Core, ejecutando los casos de uso de `Tiie.Aplicacion` contra SQL Server 2022 con un volumen sintético de unos tres años de operación.

## Dictamen

**No hace falta rediseñar.** El modelo está bien normalizado, los tipos son los correctos y lo que más importa, la reserva, ya es atómica y barata:

- `Cupo` materializado con `UPDATE` condicional y `CHECK (Ocupados BETWEEN 0 AND Capacidad)` ([ADR 0004](adr/0004-cupo-materializado.md)): una reserva completa cuesta unas 130 lecturas lógicas y menos de 5 ms de base.
- RN-01 garantizada por un índice único filtrado, idempotencia con otro índice único filtrado, folio con secuencia.
- Disponibilidad pública en una sola consulta agrupada que se resuelve con un *seek* sobre `UX_Cupo_OfertaFechaHora`: 16 lecturas para 49 días. RNF-06 tiene holgura de sobra.
- Bandeja de salida con `UPDATE TOP … WITH (READPAST, UPDLOCK, ROWLOCK)` sobre un índice filtrado de pendientes; procesos diarios con `sp_getapplock`.

**Sí había que corregir** cuatro consultas del personal y de los procesos, un tipo de columna y un script de operación. Se resolvió con una migración nueva compatible con la versión anterior del código (`OptimizacionConsultas`), cambios en dos consultas y en un proceso, y una línea en el script. La búsqueda del mostrador pasó de unos 3 s a milisegundos.

**Procedimientos almacenados: no hay y no conviene agregarlos.** Las reglas de agenda viven en `Tiie.Dominio` y `Tiie.Aplicacion` (regla 1 de [CLAUDE.md](../CLAUDE.md)); un procedimiento las duplicaría fuera de las pruebas. El sistema de 2016 sobrevendía justo por un procedimiento, `CitaInsertar`, que contaba y luego insertaba. El único SQL a mano está en `tools/` y es de operación.

## Cómo se midió

| Tabla | Filas |
| --- | --- |
| `Notificacion` | 3 459 044 |
| `Auditoria` | 2 174 286 |
| `Cita` | 2 126 465 |
| `Cupo` | 1 329 120 |
| `Solicitante` | 968 280 |
| `Oferta` | 120 (40 módulos × 3 trámites) |

- **Volumen:** generado con [`tools/volumen-sintetico.sql`](../tools/volumen-sintetico.sql). De enero de 2024 a 49 días después de hoy, de lunes a sábado, 12 horas con capacidad 3. Son unas 700 000 citas por año, una cifra a propósito alta mientras no se conozca el volumen real ([01](01-vision-y-alcance.md#preguntas-abiertas)).
- **Consultas:** un arnés de consola llamó a los manejadores reales (`ConsultarDisponibilidadManejador`, `CrearCitaManejador`, `CitasPersonalManejador`, `ReportesManejador`, procesos…). Por cada escenario se vació la caché de planes y se leyeron lecturas lógicas, tiempo y plan de `sys.dm_exec_query_stats`.
- **Condiciones:** SQL Server 2022 Developer en contenedor, nivel de compatibilidad 160, caché de datos caliente. Los milisegundos son orientativos porque Azure SQL usa otro hardware; las lecturas lógicas y la forma de los planes sí se trasladan.
- **Alcance:** un escenario «supervisor» o «capturista» usa el filtro por módulos (`IN (…)`), que es el caso normal del mostrador.

## Resultados

Lecturas lógicas por ejecución y tiempo en el cliente, antes y después de los cambios.

| Escenario | Lecturas antes | Lecturas después | ms antes | ms después |
| --- | --- | --- | --- | --- |
| Disponibilidad pública (49 días) | 16 | 16 | 15 | 16 |
| Horas de un día | 9 | 9 | 5.9 | 7.9 |
| Reserva en línea | 141 | 133 | 39 | 42 |
| Agenda del día (capturista) | 608 | 405 | 14 | 12 |
| **Búsqueda por folio** | **7 093 529** | **9** | **3 533** | **10** |
| **Búsqueda por CURP** | **7 093 801** | **66** | **3 363** | **17** |
| **Búsqueda por CURP (capturista)** | **1 159 963** | **69** | **891** | **67** |
| **Búsqueda por folio anterior** | **7 093 324** | **27** | **3 052** | **13** |
| Listado de citas sin filtros | 151 604 | 97 565 | 936 | 990 |
| Listado de citas de hoy, un módulo | 624 | 625 | 54 | 58 |
| **Detalle de cita del personal** | **36 248** | **50** | **274** | **15** |
| Tablero (supervisor, un módulo) | 3 317 | 3 318 | 83 | 76 |
| Tablero (administrador, todos) | 13 217 | 19 312 | 256 | 174 |
| **Reporte de un mes (administrador)** | **149 174** | **20 799** | **1 049** | **405** |
| Bitácora sin filtros | 8 332 | 8 372 | 52 | 50 |
| Bitácora de una semana | 226 | 226 | 10 | 9.8 |
| Bitácora de un usuario | 418 | 418 | 16 | 14 |
| Mi cita: acceso con token | 1 | 1 | 2.3 | 2.0 |
| Mi cita: consulta | 29 | 29 | 5.4 | 4.9 |
| Bandeja de salida (lote de 20) | 274 | 274 | 71 | 62 |
| Cierre diario | 21 878 | 102 934 | 404 | 319 |
| Recordatorios | 99 910 | 112 797 | 1 366 | 1 689 |
| Generación de cupos (120 ofertas) | 5 227 | 5 227 | 959 | 1 441 |
| Retención con 2 años acumulados | 21 971 162 | ver nota | 27 102 | 83 103 |

Cómo leer la tabla:

- **Milisegundos:** incluyen el trabajo de la aplicación y las idas a la base, y varían ±20 % entre corridas. Los cambios en negritas están muy por encima de esa variación.
- **Cierre diario, recordatorios y tablero de todos los módulos:** con el índice más ancho el optimizador eligió otros planes. Leen algo más, pero tardan lo mismo o menos, y los dos primeros corren de noche.
- **Retención:** el conteo de lecturas de la versión nueva queda incompleto, porque el plan se recompila mientras la tabla cambia y SQL Server reinicia sus estadísticas. Lo que importa ahí son los candados ([punto 3](#3-retención-borrado-de-notificaciones)).

## Hallazgos y cambios

### 1. Búsqueda del personal por folio, folio anterior o CURP (crítico)

La búsqueda global del panel ([17](17-experiencia-del-panel.md#principios)) es la acción más frecuente del mostrador. Se filtraba con `Folio = @b OR FolioAnterior = @b OR Solicitante.Curp = @b`. Un `OR` entre columnas de dos tablas no puede usar ningún índice, así que cada búsqueda recorría las 2.1 millones de citas: 7 millones de lecturas, unos 3 s y 10 s de CPU en paralelo. Con varias personas buscando a la vez, eso basta para saturar una base pequeña.

**Cambio** (`CitasPersonal.BuscarAsync`):

1. Primero se leen las llaves de las citas que coinciden, con tres búsquedas exactas unidas con `UNION`. Cada una usa su propio índice: `UX_Cita_Folio`, `IX_Cita_FolioAnterior`, y `UX_Solicitante_Curp` seguido de `IX_Cita_Solicitante`.
2. Después, el listado filtra por esas llaves.

Se hace en dos pasos porque, en una sola consulta, el filtro por módulos hacía que el optimizador recorriera todos los cupos de esos módulos con un *hash join*: 116 ms de CPU por búsqueda. No hace falta ningún índice nuevo.

### 2. Detalle de la cita del personal

- **Movimientos de la cita.** Se buscan en `Auditoria` por `Entidad` y `EntidadId`, y no había índice para esas columnas. Cada detalle recorría la bitácora entera, unas 33 000 lecturas con 2.2 millones de registros, y la bitácora solo crece. Se agregó `IX_Auditoria_Entidad (Entidad, EntidadId)`, sin columnas incluidas: la versión que las incluía rendía igual y ocupaba el doble (124 MB contra 64 MB).
- **Citas relacionadas.** Se pedían con un `OR` contra una subconsulta y el plan recorría `IX_Cupo_Fecha` completo. Ahora `CitaOrigenId` se lee junto con `CreadaPor`, en la consulta que ya existía para ese dato, y la búsqueda de relacionadas usa valores conocidos.

Resultado: de 36 000 lecturas y 276 ms a unas 50 lecturas y 18 ms.

### 3. Retención: borrado de notificaciones

`Retencion` borraba las notificaciones enviadas de más de 180 días con un solo `DELETE`. La primera vez que se active `Agenda:MesesRetencion` habrá años acumulados. En la prueba fueron 2.8 millones de filas: 29 s y 22 millones de lecturas, con el candado escalado a toda la tabla (`OBJECT X`, comprobado en `sys.dm_tran_locks`). Mientras dura, ninguna reserva puede insertar su confirmación en `Notificacion`, así que la reserva en línea se detiene.

**Cambio** (`Retencion.BorrarNotificacionesEnviadasAsync`): borra por lotes de 4 000 filas, cada lote en su propia sentencia, siguiendo la llave primaria desde donde terminó el anterior. Cada lote toma candados de fila y no escala. Con todo el atraso acumulado tarda más en total (80 s en el contenedor, sobre todo por la actualización de estadísticas mientras la tabla cambia), pero cada lote retiene sus candados unos 40 ms. Una ejecución semanal normal borra una semana y dura segundos.

### 4. Agenda, listados, tablero y reportes: `IX_Cita_Cupo`

Todas estas consultas llegan a la cita por su cupo. El índice solo incluía `Estado`, así que por cada fila había que volver a la tabla para leer `Folio`, `Origen` o `SolicitanteId`. En el reporte de un mes, el optimizador prefería recorrer toda la tabla `Cita`: 145 000 lecturas.

**Cambio:** `IX_Cita_Cupo (CupoId) INCLUDE (Estado, SolicitanteId, Folio, Origen)`. Ninguna de esas columnas cambia después de crear la cita, salvo `Estado`, que ya estaba incluido, así que actualizar una cita no cuesta más. El índice pasa de 31 MB a 78 MB. A cambio:

- el reporte de un mes baja de 149 000 a 21 000 lecturas;
- la agenda del día baja de 608 a 405 lecturas.

### 5. `Cita.TokenProtegido` en `varchar`

Data Protection produce Base64url, solo ASCII: 176 caracteres hoy. Guardarlo en `nvarchar(600)` ocupaba 352 bytes por cita, cerca de dos tercios de la fila.

**Cambio:** `varchar(600)`, sin pérdida de datos. La tabla `Cita` (índice clúster) pasa de 1 111 MB a 699 MB, un 37 % menos, lo que reduce cada lectura de la tabla, la memoria que ocupa en caché y los respaldos.

Ningún entorno tiene datos todavía, así que el cambio cuesta nada. Con datos, `ALTER COLUMN` reescribe la tabla y el espacio solo se recupera con `ALTER INDEX PK_Cita REBUILD`.

### 6. Índices de llaves foráneas casi vacías

`IX_Cita_CitaOrigen`, `IX_Cita_CreadaPor` e `IX_Cita_ActualizadaPor` guardaban una entrada por cita aunque la columna estuviera vacía:

- `CreadaPor` es nulo en toda reserva en línea;
- `CitaOrigenId` solo tiene valor en las reprogramadas.

**Cambio:** se filtraron con `IS NOT NULL`. La búsqueda por valor (`CitaOrigenId = @id`) sigue usándolos; se comprobó forzando el índice. Una reserva en línea deja de escribir en dos índices, y ocupan 55 MB menos.

### 7. Script `tools/reencolar-descartadas.sql`

El runbook de [13](13-despliegue-y-operacion.md#runbooks) fallaba con el error 1934 al ejecutarse con `sqlcmd` sin `-I`. `Notificacion` tiene índices filtrados, y modificarla exige `QUOTED_IDENTIFIER ON`, que `sqlcmd` deja apagado por omisión. El script ahora activa `QUOTED_IDENTIFIER` y `ANSI_NULLS` al inicio; se probó sin `-I`.

### 8. Pruebas con el aislamiento de producción

Azure SQL trae READ COMMITTED SNAPSHOT activo; SQL Server en contenedor no. Las pruebas de integración probaban la concurrencia con otro aislamiento que el de producción.

**Cambio:** `FabricaTiie` activa READ COMMITTED SNAPSHOT y una prueba lo verifica. Las 167 pruebas de integración pasan igual, incluida la de 100 reservas simultáneas por el último lugar. Aparte se comprobó que la bandeja de salida (`READPAST` con `UPDLOCK`) funciona con ese aislamiento.

## Revisado y sin cambios

| Tema | Conclusión |
| --- | --- |
| Tipos | `date`, `time(0)` y `datetimeoffset(0)` en todas partes; enumeraciones en `tinyint` con `CHECK`; CURP `char(18)`; hash del token `binary(32)`. Solo `TokenProtegido` estaba mal (punto 5) |
| Llaves e integridad | Llaves primarias `int`/`bigint IDENTITY`, que solo crecen al final; llaves foráneas en cada relación, sin cascada salvo configuración hija; `CHECK` en estados, rangos y capacidad |
| Secuencia del folio | `SecuenciaFolio` con la caché por omisión: puede saltar números tras un reinicio, lo que RN-10 permite |
| Concurrencia de la reserva | El `UPDATE` condicional reevalúa la condición con candado de actualización también con READ COMMITTED SNAPSHOT. Dos reprogramaciones cruzadas (A pasa de 1 a 2 mientras B pasa de 2 a 1) pueden causar un *deadlock* (1205); `EnableRetryOnFailure` lo reintenta y la transacción se repite completa |
| Disponibilidad y horas | Un *seek* cubierto por `UX_Cupo_OfertaFechaHora` |
| Mi cita | El acceso por token es un *seek* en `UX_Cita_TokenHash` (1 a 3 lecturas) |
| Bandeja de salida | El lote y el conteo de atrasadas usan `IX_Notificacion_Pendientes`; el filtro se reconoce aunque EF compare con `CAST(1 AS tinyint)` |
| Cierre diario y recordatorios | Menos de 2 s por noche con este volumen |
| Generación de cupos | 1.3 s para 120 ofertas, todo con *seeks* |
| Tablero | De 80 a 160 ms; sin cambios |
| Bitácora | Con filtros de fecha o usuario usa sus índices. Sin filtros, el `COUNT(*)` de la paginación recorre `IX_Auditoria_Fecha` (7 700 lecturas, unos 50 ms) y crece con la tabla. Es aceptable para una pantalla del administrador |

## Pendientes

| Tema | Propuesta | Prioridad |
| --- | --- | --- |
| Listado de citas sin filtros | Ordenar por fecha descendente, luego hora, obliga a ordenar todas las citas: entre 0.9 y 1.4 s con este volumen, y crece con la tabla. Ningún índice lo evita porque el orden mezcla columnas de `Cita` y `Cupo`. Se propone que `/admin/citas` abra con un rango por omisión (por ejemplo, de hoy a 7 días) y, si negocio lo acepta, que la API exija rango cuando no hay búsqueda | Should |
| Medir con el volumen real | Repetir este análisis con la consulta de volumen del sistema de 2016 y, en QA, con la prueba de carga de [12](12-pruebas.md) sobre el SKU real | Must antes de producción |
| Retención de la bitácora | `Auditoria` no tiene plazo y es la tabla que más crece después de `Notificacion`. Definir el plazo legal; cuando exista, borrar por lotes como en el punto 3. Particionar solo si pasa de decenas de millones de filas | Should |
| Capacidad del folio | El folio usa 7 dígitos (`D7`) en `varchar(14)` y la secuencia empieza en 1 000 000: caben unos 9 millones de citas, más de 12 años con el volumen supuesto. Antes de llegar, ampliar a 8 dígitos y `varchar(15)` con una migración | Could |
| Estadísticas en Azure SQL | Activar `AUTO_UPDATE_STATISTICS_ASYNC`, para que un borrado grande no detenga consultas esperando estadísticas nuevas. Revisar Query Store y el ajuste automático (`FORCE_LAST_GOOD_PLAN`, activo por omisión) después de cada despliegue | Could |

## Cómo repetir la medición

1. Crear una base vacía en el SQL Server local y aplicarle el script de migraciones.
2. Cargar [`tools/volumen-sintetico.sql`](../tools/volumen-sintetico.sql). Las instrucciones están en su encabezado; se niega a correr si la base ya tiene citas.
3. Revisar las consultas con `sys.dm_exec_query_stats` o con el plan real (`SET STATISTICS XML ON`). Compararlas siempre por lecturas lógicas, que no dependen del hardware.
