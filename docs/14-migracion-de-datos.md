# 14 · Migración de datos

Se migran catálogos, plantillas, usuarios y citas futuras al modelo nuevo con una herramienta repetible. Las citas pasadas se migran solo como totales diarios, sin datos personales. La herramienta se ensaya al menos dos veces sobre copias de producción antes del corte.

## Antes de empezar

1. Obtener el código desplegado del sistema de 2016 y confirmar que la base de producción es la del script analizado (catálogo `TIEDev`).
2. Ejecutar las consultas de verificación del análisis. Sus resultados deciden el mapa de roles, cuántos duplicados hay que resolver y qué horas revisar.
3. Confirmar con operación qué reglas del JavaScript anterior siguen vigentes: sábados cerrados para los trámites 21, 22 y 23, cierres del módulo 9 y días inhábiles.

## Herramienta

`tools/Tiie.Migracion`, una aplicación de consola .NET 10 que lee la base anterior con `Microsoft.Data.SqlClient` y escribe en la nueva con `TiieDbContext`.

```bash
# Diagnóstico de la base anterior: no escribe nada
dotnet run --project tools/Tiie.Migracion -- analizar --origen "<cadena 2016>"

# Migración; repetible: actualiza lo que ya existe usando IdAnterior y FolioAnterior
dotnet run --project tools/Tiie.Migracion -- migrar \
  --origen "<cadena 2016>" --destino "<cadena v2>" \
  --fecha-corte 2026-11-30 \
  --mapa-roles "0=Capturista,1=Supervisor,2=Administrador" \
  --decisiones decisiones-duplicados.csv

# Comparación de conteos entre origen y destino
dotnet run --project tools/Tiie.Migracion -- conciliar --origen "<cadena 2016>" --destino "<cadena v2>"
```

- `--mapa-roles` es obligatorio: la herramienta no adivina qué significa cada valor de `Usuario.Tipo`.
- `--fecha-corte` es el día del corte; las citas con `FechaCita` igual o posterior se migran como vigentes.
- Los reportes quedan en `artifacts/migracion/<fecha>/`. Contienen datos personales: se generan solo en un equipo autorizado y se borran después del corte.

## Mapeo

### Catálogos

| Origen | Destino | Reglas |
| --- | --- | --- |
| `Entidad` | `Entidad` | Mismo ID; `Entidad` → `Nombre`; `Estatus` → `Activo` |
| `Municipio` | `Municipio` | Mismo ID; `Municipio` → `Nombre` |
| `Modulo` | `Modulo` | `Poblacion` → `Localidad`; `NumExt` vacío → «S/N»; `Email` → `Correo`; `Estatus` → `Activo`; `ModuloId` → `IdAnterior`. `EntidadId` se descarta: sale del municipio |
| `Tramite` | `TipoTramite` | Se agrupan por nombre normalizado (sin acentos, mayúsculas, espacios simples). `Detalle` → `Descripcion`, `Costo` y `Notas`: el valor más común del grupo |
| `Tramite.Requisitos` | `Requisito` | Un solo requisito por tipo, sin nivel escolar, con el texto más común. Se reporta para reestructurarlo por nivel en el panel |
| `Tramite` | `Oferta` | Una oferta por fila con módulo válido. `TramiteId` → `IdAnterior`; `DiasVentana = 49`; `Activo` = trámite y módulo activos. Si los requisitos o notas difieren del tipo, la diferencia va a `NotasSede` y al reporte |
| `Horario` | `PlantillaHorario` | Por cada horario activo con turnos: lunes a viernes con la misma hora y turnos; sábado solo si la hora está entre 09:00 y 12:30 y el trámite no es 21, 22 ni 23; domingo nunca |
| `Requisito`, `Reporte` (tablas sin uso) | — | Se revisa su contenido; `Reporte` sirve para cruzar los totales históricos |

Los horarios con hora menor a 07:00 o mayor a 19:00 van a `horas-sospechosas.csv`: el panel anterior guardaba las horas PM como AM. Operación corrige la hora antes del corte.

### Citas futuras

Citas con `Estatus = 'VIGENTE'` y `FechaCita` igual o posterior a la fecha de corte, unidas con `HorarioCita` y `Horario`.

| Origen | Destino | Reglas |
| --- | --- | --- |
| `Cita.CURP` y datos personales | `Solicitante` | Un solicitante por CURP normalizada, con los datos de la cita más reciente. Una CURP que no pasa la validación se migra igual y se reporta |
| `HorarioCita` + `Horario` | `Cupo` | Un cupo por oferta, fecha y hora. `Ocupados` = citas migradas; `Capacidad` = mayor entre los turnos y las citas |
| `Cita` | `Cita` | Estado Agendada; origen Migración; folio nuevo; folio anterior en `FolioAnterior`; token nuevo; sin aviso de privacidad aceptado |
| — | `Notificacion` | Una «Aviso de migración» por cita, con el enlace nuevo y el comprobante |

- Una CURP con varias citas futuras rompe RN-01. Van a `duplicados-curp.csv` y operación decide cuál conservar en `decisiones-duplicados.csv`. Sin decisión, la herramienta no migra esas citas y lo informa.
- Las citas cuyo horario ya no existe (registros huérfanos) van a `excepciones.csv` y no se migran sin revisión.
- Un cupo con más citas que turnos se migra con capacidad igual a las citas y se marca en el reporte; ninguna cita se cancela de forma automática.

### Citas pasadas

Se suman por fecha y oferta en `HistoricoCita`. Los datos personales de esas citas no pasan a la versión 2; quedan en la base anterior archivada, sujetos a la política de retención.

### Usuarios

| Origen | Destino | Reglas |
| --- | --- | --- |
| `Usuario` activo | `Usuario` | Mismo `UserName` y correo; rol según `--mapa-roles`; sin contraseña; `EmailConfirmed = false` |
| `Usuario` inactivo | — | No se migra |
| `Usuario.ModuloId` | `UsuarioModulo` | Si tiene valor, se asigna; si no, se reporta para asignarlo en el panel |

Después de migrar, cada usuario recibe la invitación de activación. Las contraseñas anteriores no se reutilizan: su clave de cifrado está expuesta en el repositorio anterior.

### Reglas del JavaScript anterior

| Regla anterior | Destino |
| --- | --- |
| Sábados cerrados para los trámites 21, 22 y 23 | Plantillas sin sábado para esas ofertas |
| Cierre del Polideportivo (módulo 9) por rango de fechas | `Bloqueo` del módulo, solo si el rango sigue vigente |
| Días inhábiles escritos en el código | `Bloqueo` general para los que estén por venir |

## Conciliación

| Verificación | Esperado |
| --- | --- |
| Módulos activos | Igual que en el origen |
| Ofertas | Trámites con módulo válido |
| Horas de plantilla de lunes a viernes por oferta | Horarios activos del trámite |
| Citas futuras migradas | Citas futuras del origen menos las excepciones aprobadas |
| Suma de `Ocupados` en cupos futuros | Citas futuras migradas |
| Solicitantes con cita Agendada | CURPs distintas de las citas futuras migradas |
| Totales históricos por mes | Conteo del origen por mes, salvo huérfanos reportados |
| Usuarios activos | Igual que en el origen |

Cualquier diferencia no explicada por un reporte detiene el corte.

## Ensayos

| Ensayo | Base de origen | Objetivo |
| --- | --- | --- |
| 1 | Copia de producción de la Fase 3 | Probar el mapeo y llenar los reportes |
| 2 | Copia de producción reciente | Medir la duración del corte y validar con operación las correcciones |
| Final | Producción en el corte | Migración definitiva |

## Corte

| Momento | Acción |
| --- | --- |
| 7 días antes | Avisar la ventana de mantenimiento en el sitio anterior; congelar cambios de catálogos y horarios |
| 1 día antes | Ensayo final; bajar el TTL del DNS a 300 s; confirmar plantillas, bloqueos y usuarios en la versión 2 |
| Hora 0 | Página de mantenimiento en el sitio anterior; respaldo `.bacpac` final |
| Hora 0 + 15 min | Migración final y conciliación |
| Hora 0 + 45 min | Prueba de humo en producción con una oferta oculta: reservar, consultar y cancelar |
| Hora 0 + 60 min | Cambio de DNS; el sitio nuevo queda público |
| Hora 0 + 75 min | Encolar los «Aviso de migración»; vigilar la bandeja de salida |
| 72 horas después | Monitoreo reforzado; la base anterior pasa a solo lectura |

## Reversión

La decisión de revertir se toma antes de abrir el sitio al público, en la prueba de humo. Si la conciliación o la prueba de humo fallan:

1. El DNS sigue apuntando al sitio anterior.
2. Se retira la página de mantenimiento del sitio anterior.
3. Se analiza la falla y se agenda un nuevo corte.

Después del cambio de DNS y de las primeras reservas en la versión 2, ya no se vuelve al sistema anterior: los problemas se corrigen hacia adelante.
