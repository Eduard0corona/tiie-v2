# 02 · Requerimientos

20 requerimientos funcionales y 14 no funcionales. Cada funcional indica su prioridad, la fase en que se entrega ([plan](15-plan-de-implementacion.md)) y sus criterios de aceptación; las reglas `RN-xx` están en [03-reglas-de-negocio.md](03-reglas-de-negocio.md).

## Resumen

| ID | Requerimiento | Prioridad | Fase |
| --- | --- | --- | --- |
| RF-01 | Consultar sedes, trámites y requisitos | Alta | 2 |
| RF-02 | Ver disponibilidad por día | Alta | 2 |
| RF-03 | Reservar una cita | Alta | 2 |
| RF-04 | Confirmación y comprobante | Alta | 2 |
| RF-05 | Mi cita: consultar, cancelar y reprogramar | Alta | 2 |
| RF-06 | Recuperar el enlace de la cita | Media | 2 |
| RF-07 | Recordatorio | Media | 2 |
| RF-08 | Aviso de privacidad | Alta | 2 |
| RF-09 | Agenda del día y asistencia | Alta | 3 |
| RF-10 | Cita en ventanilla | Media | 3 |
| RF-11 | Cancelar o reprogramar por el personal | Media | 3 |
| RF-12 | Módulos | Alta | 3 |
| RF-13 | Tipos de trámite, requisitos y ofertas | Alta | 3 |
| RF-14 | Plantillas de horario | Alta | 3 |
| RF-15 | Bloqueos y días inhábiles | Alta | 3 |
| RF-16 | Usuarios del personal | Alta | 1 y 3 |
| RF-17 | Reportes | Alta | 3 |
| RF-18 | Tablero | Media | 3 |
| RF-19 | Bitácora de auditoría | Alta | 1 y 3 |
| RF-20 | Procesos automáticos | Media | 2 y 3 |

## Sitio público

### RF-01 · Consultar sedes, trámites y requisitos

- La página de inicio muestra los tipos de trámite activos con su costo y sus requisitos agrupados por nivel escolar.
- La lista de sedes muestra municipio, dirección, horario de atención y un enlace «Cómo llegar» que abre el mapa del dispositivo con las coordenadas del módulo.
- Un módulo, tipo de trámite u oferta inactivos no aparecen en ningún punto del sitio público.

### RF-02 · Ver disponibilidad por día

- Al elegir una oferta, el calendario muestra cada día entre mañana y el final de la ventana con uno de cinco estados: Alta, Poca, Llena, Cerrado o Sin servicio (RN-03, RN-07).
- Los días Llena, Cerrado y Sin servicio se ven deshabilitados y no se pueden elegir.
- Al elegir un día se listan las horas con lugar y cuántos lugares quedan.
- La disponibilidad de una ventana completa responde en menos de 500 ms en el percentil 95.

### RF-03 · Reservar una cita

- Con un cupo con lugar y una CURP válida sin cita activa, al confirmar se crea la cita en estado Agendada, se ocupa un lugar y se muestra el folio (RN-01, RN-02, RN-09).
- Si el cupo se llenó mientras la persona capturaba, la respuesta es `SinCupo` y la pantalla ofrece otras horas del mismo día.
- Si la CURP ya tiene una cita Agendada, la respuesta es `YaTieneCita` y la pantalla ofrece recuperar el enlace por correo.
- La fecha de nacimiento y el sexo no se piden: se derivan de la CURP (RN-15).
- No se puede enviar sin aceptar el aviso de privacidad ni sin un captcha válido.
- Repetir la misma solicitud con la misma cabecera `Idempotency-Key` devuelve la misma cita, no un error.

### RF-04 · Confirmación y comprobante

- La confirmación muestra folio, fecha, hora, sede, dirección, trámite, requisitos y notas.
- El comprobante PDF se descarga desde la confirmación y desde Mi cita y contiene la misma información.
- El correo de confirmación incluye el enlace privado y el PDF adjunto.
- Si el envío del correo falla, la cita no se revierte y la persona no ve un error; el correo se reintenta ([10](10-notificaciones-y-procesos.md)).

### RF-05 · Mi cita: consultar, cancelar y reprogramar

- El enlace del correo abre Mi cita sin pedir datos (RN-16).
- Muestra estado, folio, fecha, hora, sede, trámite y requisitos; la CURP y el correo aparecen enmascarados.
- Permite cancelar y reprogramar dentro del plazo (RN-13, RN-17). Fuera del plazo, los botones no aparecen y la API responde `CitaNoModificable`.
- Un enlace vencido o inválido muestra «Este enlace ya no es válido» con la opción de recuperarlo.

### RF-06 · Recuperar el enlace de la cita

- Con CURP y correo, si existe una cita Agendada cuyo correo coincide, se envía el enlace a ese correo.
- La respuesta y el mensaje en pantalla son idénticos exista o no la cita.
- Se aceptan como máximo 3 solicitudes por CURP por hora.

### RF-07 · Recordatorio

- A las 09:00 del día anterior se envía un recordatorio a cada cita Agendada (RN-14).
- Una cita recibe a lo sumo un recordatorio.

### RF-08 · Aviso de privacidad

- El aviso simplificado aparece en el paso de datos, con enlace al aviso integral.
- La cita guarda la versión del aviso aceptada y el momento de la aceptación.

## Panel del personal

### RF-09 · Agenda del día y asistencia

- El personal ve las citas de un módulo y una fecha, ordenadas por hora; por omisión, hoy y su primer módulo asignado.
- Puede marcar Atendida o No asistió en una cita Agendada de hoy o de días anteriores, nunca de días futuros.
- Puede buscar por folio, folio anterior o CURP dentro de sus módulos.

### RF-10 · Cita en ventanilla

- Aplica las mismas reglas que la reserva pública, sin captcha, y puede usar cupos de hoy con hora posterior a la actual (RN-18).
- La cita queda con origen Ventanilla y con el usuario que la registró.
- El correo es opcional: si se captura, se envía la confirmación; si no, el personal imprime el comprobante.

### RF-11 · Cancelar o reprogramar por el personal

- Exige un motivo, libera el lugar y notifica al ciudadano si hay correo.
- El personal puede hacerlo hasta la hora de la cita; RN-13 no aplica al personal.

### RF-12 · Módulos

- Solo el administrador da de alta y edita módulos.
- Un módulo con citas no se borra: se desactiva, deja de ofrecerse y se listan sus citas futuras para decidir qué hacer con ellas (RN-11).

### RF-13 · Tipos de trámite, requisitos y ofertas

- Los requisitos son textos largos por nivel escolar, ordenables, sin límite práctico de longitud.
- Una oferta une un módulo y un tipo de trámite; su ventana se configura entre 1 y 120 días.
- Los cambios aparecen en el sitio público en menos de un minuto.

### RF-14 · Plantillas de horario

- Se edita la semana completa de una oferta: por cada día, horas y turnos.
- Al guardar, se regeneran los cupos futuros y se muestra un resumen: cupos creados, actualizados y cupos con citas que no cambiaron (RN-04).

### RF-15 · Bloqueos y días inhábiles

- Un bloqueo tiene rango de fechas, alcance (general, módulo u oferta) y motivo (RN-07, RN-08).
- Al crearlo, los cupos del rango se cierran y se listan las citas afectadas, con la opción de cancelarlas en bloque y avisar a cada persona.
- Eliminar un bloqueo reabre los cupos que no tengan otro bloqueo encima.

### RF-16 · Usuarios del personal

- El administrador crea usuarios con rol y módulos; el usuario recibe un correo para activar su cuenta y definir su contraseña.
- En el primer inicio de sesión se obliga a configurar MFA.
- Desactivar un usuario cierra sus sesiones en menos de 5 minutos.
- El administrador puede restablecer el MFA de un usuario y reenviar la invitación.

### RF-17 · Reportes

- Filtros: rango de fechas inclusivo, módulo, tipo de trámite y estado.
- Exporta a Excel y PDF con las mismas filas que la pantalla.
- Un supervisor solo ve sus módulos.

### RF-18 · Tablero

- Muestra citas de hoy por estado, ocupación de los próximos 7 días e inasistencia de los últimos 30 días por módulo.

### RF-19 · Bitácora de auditoría

- Registra inicios y cierres de sesión, intentos fallidos, cambios de catálogos, plantillas, bloqueos y usuarios, y cada cambio de estado de una cita hecho por el personal.
- Cada registro guarda fecha, usuario, acción, entidad, identificador y cambios, sin datos personales completos.
- Solo el administrador la consulta. Nadie la edita ni la borra desde la aplicación.

### RF-20 · Procesos automáticos

- Cierre diario: las citas Agendada con fecha pasada pasan a No asistió (RN-12).
- Generación diaria de cupos para toda la ventana (RN-02).
- Retención: anonimiza solicitantes según el plazo configurado; desactivada por omisión.

## No funcionales

Los objetivos numéricos son propuestas; se ajustan cuando se conozca el volumen real.

| ID | Requerimiento | Objetivo |
| --- | --- | --- |
| RNF-01 | Seguridad de la aplicación | OWASP ASVS nivel 2; HTTPS con HSTS; cabeceras de seguridad y CSP ([11](11-seguridad-y-privacidad.md)) |
| RNF-02 | Secretos | Cero secretos en el repositorio; Key Vault e identidad administrada hacia Azure SQL |
| RNF-03 | Privacidad | Ley general y ley de Sinaloa de protección de datos personales en posesión de sujetos obligados; datos mínimos; acceso a la cita solo con token |
| RNF-04 | Integridad de la agenda | Cero sobreventa con solicitudes simultáneas; reglas validadas en el servidor |
| RNF-05 | Protección contra bots | Captcha y límites por IP y por CURP |
| RNF-06 | Rendimiento | Disponibilidad en menos de 500 ms y reserva en menos de 1 s, percentil 95, con la carga pico |
| RNF-07 | Disponibilidad del servicio | 99.5 % mensual |
| RNF-08 | Accesibilidad | WCAG 2.1 nivel AA; diseño pensado primero para celular |
| RNF-09 | Fechas y horas | Reglas calculadas en `America/Mazatlan`; instantes guardados en UTC |
| RNF-10 | Observabilidad | Logs estructurados, trazas y alertas; cada operación de una cita rastreable por su folio |
| RNF-11 | Mantenibilidad | Git, CI/CD y pruebas automáticas; 90 % de cobertura en `Tiie.Dominio` |
| RNF-12 | Respaldo | Restauración a un punto en el tiempo de 35 días en producción |
| RNF-13 | Navegadores | Dos últimas versiones de Chrome, Edge, Firefox y Safari; Chrome en Android 10 o superior; Safari en iOS 16 o superior |
| RNF-14 | Rendimiento del frontend | JavaScript inicial del sitio público menor a 200 KB comprimido; LCP menor a 2.5 s en 4G simulada |
