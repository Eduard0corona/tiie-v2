# Documentación de TIIE v2

Esta carpeta es la referencia para construir la versión 2. El análisis del sistema de 2016 que motivó estas decisiones está en el documento «TIIE v2 — Análisis del sistema actual y especificación de la nueva versión».

| # | Documento | Contenido |
| --- | --- | --- |
| 01 | [Visión y alcance](01-vision-y-alcance.md) | Propósito, alcance, actores, glosario, decisiones tomadas y preguntas abiertas |
| 02 | [Requerimientos](02-requerimientos.md) | Requerimientos funcionales con criterios de aceptación y no funcionales |
| 03 | [Reglas de negocio](03-reglas-de-negocio.md) | Reglas RN-01 a RN-18, estados de la cita, disponibilidad, cupos, folio, CURP |
| 04 | [Arquitectura](04-arquitectura.md) | Un solo sitio ASP.NET Core con React, capas, flujos y configuración |
| 05 | [Modelo de datos](05-modelo-de-datos.md) | Tablas, columnas, índices, restricciones y datos semilla |
| 06 | [API](06-api.md) | Convenciones, códigos de error y contrato de cada endpoint |
| 07 | [Autenticación y autorización](07-autenticacion-y-autorizacion.md) | Identity, MFA, roles, alcance por módulo, CSRF y token del ciudadano |
| 08 | [Frontend](08-frontend.md) | React: estructura, rutas, estado, formularios, fechas, accesibilidad y build |
| 09 | [Backend](09-backend.md) | Proyectos, patrones, EF Core, errores, logging y OpenAPI |
| 10 | [Notificaciones y procesos](10-notificaciones-y-procesos.md) | Bandeja de salida de correos, plantillas y procesos programados |
| 11 | [Seguridad y privacidad](11-seguridad-y-privacidad.md) | Amenazas y controles, cabeceras, límites, datos personales |
| 12 | [Pruebas](12-pruebas.md) | Estrategia, pruebas obligatorias, concurrencia, E2E y carga |
| 13 | [Despliegue y operación](13-despliegue-y-operacion.md) | Recursos de Azure, CI/CD, migraciones, monitoreo y runbooks |
| 14 | [Migración de datos](14-migracion-de-datos.md) | Mapeo desde la base de 2016, ensayos y corte |
| 15 | [Plan de implementación](15-plan-de-implementacion.md) | Fases, épicas, historias y definición de terminado |
| 16 | [Experiencia de usuario](16-experiencia-de-usuario.md) | Diseño de la portada y del servicio de citas: personas, principios, sistema de diseño, pantallas, contenido editable, métricas y backlog |
| 17 | [Experiencia del panel](17-experiencia-del-panel.md) | Diseño de `/admin`: marco, búsqueda global, agenda del día, tablero, portada del sitio, componentes del panel y backlog |
| 18 | [Análisis de la base de datos](18-analisis-de-base-de-datos.md) | Revisión de estructura, índices, consultas y scripts con medición sobre volumen sintético; dictamen y cambios |
| — | [Decisiones de arquitectura (ADR)](adr/README.md) | Registro de decisiones y su contexto |

## Orden de lectura sugerido

- **Quien se integra al equipo:** 01, 04, 03 y [CLAUDE.md](../CLAUDE.md).
- **Backend:** 03, 05, 06, 07, 09, 10 y 18.
- **Frontend:** 03, 06, 07 (secciones de CSRF y token del ciudadano), 08 y 16.
- **Producto y diseño:** 01, 02, 16, 17 y 15.
- **Operación y despliegue:** 11, 13 y 14.

## Cómo se mantiene

- Un PR que cambia una regla, un endpoint o el esquema actualiza el documento correspondiente en el mismo PR.
- Una decisión que cambia la arquitectura se registra como ADR nuevo. Los ADR aceptados no se editan: se reemplazan con otro que los declara superados.
- Lo marcado como **«por confirmar»** necesita una decisión de negocio. Todas esas preguntas están en [01-vision-y-alcance.md](01-vision-y-alcance.md#preguntas-abiertas), cada una con el valor que se usa mientras tanto.
