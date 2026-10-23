# 01 · Visión y alcance

## Propósito

Que cualquier estudiante, o su madre, padre o tutor, agende desde el celular una cita para tramitar la TIIE sin sobreventa ni exposición de sus datos, y que el personal de cada módulo trabaje con una agenda confiable en la que registra la asistencia.

## Contexto

La versión de 2016 tiene credenciales en el código, páginas administrativas sin control de rol, datos personales consultables con solo la CURP, reglas de agenda validadas en el navegador, sobreventa por concurrencia y errores de zona horaria. El detalle está en el documento de análisis. La versión 2 se construye desde cero y reutiliza solo los catálogos y las citas vigentes.

## Objetivos

| Objetivo | Indicador | Meta propuesta |
| --- | --- | --- |
| Agenda confiable | Citas por encima de la capacidad de un cupo | 0 |
| Privacidad | Rutas que devuelven datos personales sin token ni sesión | 0 |
| Reserva sencilla | Tiempo para completar una reserva en celular, medido con usuarios | Menos de 3 minutos |
| Operación medible | Citas del día con asistencia registrada | 95 % o más |
| Avisos confiables | Correos de confirmación entregados al proveedor en menos de 5 minutos | 99 % |

## Alcance de la versión 2

**Sitio público**

- Información del trámite, requisitos por nivel escolar y sedes.
- Reserva en cuatro pasos: sede y trámite, fecha, hora, y datos personales.
- Confirmación con folio, comprobante PDF y correo con un enlace privado.
- «Mi cita»: ver, descargar el comprobante, cancelar y reprogramar.
- Recuperar el enlace por correo.
- Aviso de privacidad.

**Panel `/admin`**

- Inicio de sesión con MFA, activación de cuenta y restablecimiento de contraseña.
- Agenda del día por módulo con registro de asistencia.
- Citas: búsqueda, detalle, alta en ventanilla, cancelación, reprogramación y reenvío del correo.
- Catálogos: módulos, tipos de trámite, requisitos y ofertas.
- Horarios: plantillas semanales, bloqueos y consulta de cupos.
- Usuarios del personal, reportes, tablero y bitácora de auditoría.

**Procesos automáticos:** envío de correos con reintentos, generación de cupos, recordatorios, cierre diario y retención de datos.

## Fuera de alcance

| Tema | Motivo |
| --- | --- |
| Prerregistro con carga de documentos | Requiere definir la integración con el servicio de credencialización; fase posterior |
| Pago en línea | El costo solo se informa |
| SMS o WhatsApp | El modelo de notificaciones admite más canales; se agregan después |
| Aplicación móvil nativa | El sitio es responsivo y está pensado primero para celular |
| Otras entidades federativas | El modelo lo permite, pero la operación es solo de Sinaloa |
| Inicio de sesión con Microsoft Entra ID | Se decidió ASP.NET Core Identity; Entra puede agregarse como proveedor externo después |

## Actores

| Actor | Descripción |
| --- | --- |
| Ciudadano | Estudiante o tutor que agenda. No tiene cuenta. |
| Capturista | Personal de módulo: agenda del día, asistencia y citas de ventanilla de sus módulos. |
| Supervisor | Lo del capturista, más ofertas, plantillas, bloqueos y reportes de sus módulos. |
| Administrador | Todo: usuarios, módulos, catálogos, días inhábiles y auditoría, en todos los módulos. |
| Sistema | Procesos programados: correos, cupos, recordatorios, cierre diario y retención. |

## Decisiones tomadas

| Decisión | Elección | ADR |
| --- | --- | --- |
| Backend | ASP.NET Core 10 como monolito modular | [0001](adr/0001-monolito-modular-aspnet-core.md) |
| Frontend y sitio | React servido por el mismo sitio, con la parte pública y `/admin` | [0002](adr/0002-react-spa-en-el-mismo-sitio.md) |
| Identidad del personal | ASP.NET Core Identity con cookies y MFA obligatoria | [0003](adr/0003-aspnet-core-identity-con-cookies-y-mfa.md) |
| Evitar la sobreventa | Cupo materializado con actualización condicional | [0004](adr/0004-cupo-materializado.md) |
| Acceso del ciudadano | Token por correo, sin cuenta | [0005](adr/0005-acceso-ciudadano-por-token.md) |
| Fechas | Zona `America/Mazatlan` y `TimeProvider` | [0006](adr/0006-zona-horaria-america-mazatlan.md) |
| Correos y procesos | Bandeja de salida en SQL y procesos dentro del mismo sitio | [0007](adr/0007-outbox-y-procesos-en-el-sitio.md) |

## Preguntas abiertas

Cada pregunta tiene un valor predeterminado para no detener el desarrollo. Se cambia por configuración o por datos cuando se decida.

| Pregunta | Valor mientras se decide | Dónde se cambia |
| --- | --- | --- |
| ¿Una cita activa por CURP, o por CURP y tipo de trámite? | Por CURP (RN-01) | Índice único filtrado; requiere migración |
| ¿Hasta cuándo puede el ciudadano cancelar o reprogramar? | Hasta las 23:59 del día anterior (RN-13) | `Agenda:DiasAnticipacionCambios` |
| ¿Se reprograma solo dentro de la misma oferta? | Sí (RN-17) | Caso de uso `ReprogramarCita` |
| ¿Se atiende en domingo? | No: plantillas sin domingo (RN-06) | Datos de la plantilla |
| ¿Qué datos se piden si el titular es menor de edad? | Los del estudiante; correo y celular pueden ser del tutor | Texto del formulario y del aviso |
| ¿Cuánto tiempo se conservan los datos personales? | Retención desactivada hasta definir el plazo | `Agenda:MesesRetencion` |
| ¿Qué volumen pico se espera? | Dimensionar con el histórico (consulta 9 del análisis) | Plan de App Service y SKU de Azure SQL |
| ¿Qué reportes usa hoy la operación? | Citas por rango, módulo, trámite y estado | Historias de la Fase 3 |
| ¿Cuál es el dominio oficial y quién lo administra? | `tarjetainteligentesinaloa.org.mx`, por confirmar | Configuración de App Service y DNS |
| ¿Qué licencia de QuestPDF corresponde? La Community solo aplica si la organización cumple sus términos | Community en desarrollo y QA; producción sin valor predeterminado: el sitio no arranca hasta decidirlo | `Documentos:LicenciaQuestPdf` y `licenciaQuestPdf` en `infra/prod.bicepparam` |
| ¿Dónde deben residir los datos del servicio de correo? | `United States`; confirmar si Communication Services ofrece una ubicación aceptable más cercana | Parámetro `ubicacionDatosCorreo` de `infra/main.bicep` |
| ¿Cómo se leen los correos en las pruebas E2E de QA? | Se omiten los flujos que necesitan el buzón; localmente se leen de la carpeta del proveedor `Archivo` | Ayudante de correo de `tests/e2e` |
| ¿Cuáles son los lineamientos de identidad gráfica del Gobierno de Sinaloa (logotipo, tipografía, imagen de la TIIE)? | Guinda `#7b1e3a`, tipografía del sistema e ilustración estilizada de una tarjeta ([16](16-experiencia-de-usuario.md#preguntas-abiertas-de-diseño)) | Tokens de `src/styles/index.css` y `components/Marca.tsx` |
| ¿Cuánto dura la atención en el módulo? | 30 minutos, solo para el evento de calendario de Mi cita | `DURACION_MINUTOS` en `features/publico/mi-cita/calendario.ts` |

## Glosario

| Término | Significado |
| --- | --- |
| Módulo | Sede física de atención. |
| Tipo de trámite | Trámite del catálogo, por ejemplo primera vez, sustitución o reactivación. |
| Nivel escolar | Agrupa requisitos: básico (preescolar, primaria y secundaria) o medio superior y superior. |
| Oferta | Un tipo de trámite que se atiende en un módulo. Tiene plantilla, cupos y ventana. En el análisis se llamó `TramiteModulo`. |
| Plantilla | Horas y turnos de una oferta para cada día de la semana. |
| Cupo | Lugares de una oferta en una fecha y hora concretas. Se genera desde la plantilla. |
| Bloqueo | Cierre por rango de fechas: general (día inhábil), de un módulo o de una oferta. |
| Ventana | Días hacia adelante en los que se puede reservar. |
| Solicitante | Persona identificada por su CURP. |
| Folio | Identificador legible de la cita, con formato `AAMMDD-NNNNNNN`. |
| Token de cita | Secreto aleatorio enviado por correo que da acceso a «Mi cita». |
| Ventanilla | Cita registrada por el personal en el módulo, en nombre del ciudadano. |
| Hoy | Fecha actual en la zona `America/Mazatlan`, nunca la del servidor. |
