# 0001 · Monolito modular en ASP.NET Core 10

- **Estado:** Aceptado
- **Fecha:** 2026-10-22

## Contexto

El sistema de 2016 está en ASP.NET WebForms sobre .NET Framework 4.5, sin soporte desde 2016. El equipo conoce C# y la base de datos ya está en Azure SQL. El dominio es pequeño (catálogos, agenda, citas, personal) y el volumen es el de un trámite estatal con picos de temporada. El equipo de desarrollo será de una a tres personas.

## Decisión

Construir la versión 2 como un solo despliegue en ASP.NET Core 10 (LTS), separado en proyectos por responsabilidad: `Tiie.Dominio`, `Tiie.Aplicacion`, `Tiie.Infraestructura` y `Tiie.Web`. Las fronteras entre proyectos se verifican con pruebas de arquitectura.

## Consecuencias

- Un solo artefacto que compilar, desplegar, monitorear y respaldar.
- Las reglas de agenda quedan en un dominio sin dependencias, fácil de probar.
- La disciplina de capas depende de las pruebas de arquitectura y de la revisión de código, no de fronteras de red.
- Si en el futuro una parte necesita escalar o desplegarse por separado, los proyectos ya están delimitados.

## Alternativas consideradas

- **Microservicios:** más infraestructura, más puntos de falla y más costo operativo, sin un beneficio claro para este tamaño de dominio y de equipo.
- **Backend en Node.js:** el equipo y la base de conocimiento están en .NET; cambiaría el lenguaje sin resolver ningún problema identificado.
- **Mantener y parchar WebForms:** la plataforma no tiene soporte y los problemas de diseño (reglas en el navegador, sin integridad referencial) exigen reescribir buena parte de todos modos.
