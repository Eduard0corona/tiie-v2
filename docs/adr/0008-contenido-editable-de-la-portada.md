# 0008 · Contenido de la portada editable en la base, como texto plano con enlaces

- **Estado:** Propuesto
- **Fecha:** 2026-10-26

## Contexto

La portada pública tenía todos sus textos en el código de React. Cambiar una pregunta frecuente o anunciar un cierre (un día inhábil, el inicio de una temporada) exigía un PR y un despliegue. El personal administrador necesita hacerlo el mismo día y sin ayuda técnica.

Las fuerzas son tres:

- Es el sitio oficial del Gobierno del Estado. Un texto editable no puede convertirse en una vía para inyectar HTML, scripts o enlaces a sitios que imitan al oficial.
- La portada es la página con más tráfico y su primera pantalla debe aparecer rápido y sin saltos.
- Las migraciones deben ser compatibles con la versión anterior del código ([CLAUDE.md](../../CLAUDE.md), regla 10).

## Decisión

- **Almacenamiento:** el contenido editable se guarda en `ContenidoPortada` (una sola fila con `rowversion`) y `PreguntaFrecuente`.
- **API:** se lee con `GET /api/publico/portada` y lo edita solo el administrador con `GET` y `PUT /api/admin/portada`.
- **Alcance:**
  - Se editan el encabezado, un aviso con fechas y las preguntas frecuentes.
  - Lo que describe cómo funciona el sitio (pasos, botones, beneficios) sigue en el código.
- **Formato:** los textos son planos. Los enlaces se escriben `[texto](destino)` y solo admiten `/ruta`, `https://`, `mailto:` y `tel:`.
- **Validación en los dos lados:** la regla vive en `ReglasPortada` (dominio), que valida al guardar, y la SPA la repite al mostrar el texto. Lo que no coincide se muestra como texto; nunca se interpreta HTML.
- **Vigencia del aviso:** la decide el backend con `RelojAgenda`, en la hora de Sinaloa.
- **Contenido predeterminado:** vive en el código (`PortadaPredeterminada`) y se muestra mientras no exista la fila. No se siembra en la migración.

## Consecuencias

- Cambiar la portada ya no requiere desplegar, y cada guardado queda en la bitácora con lo anterior y lo nuevo (`Portada.Guardar`).
- La migración solo agrega tablas vacías, así que el código anterior y el nuevo conviven. Al desplegar, la portada se ve igual que antes.
- La portada hace una petición más. Mientras llega, el encabezado muestra barras del mismo alto. Si falla, conserva su título y sus botones.
- El formato de enlaces es propio y pequeño. Si algún día hace falta negritas o listas, se amplía aquí con la misma lista blanca, no con HTML.
- Dos administradores que editan a la vez se detectan con la versión: el segundo recibe 409 y recarga.

## Alternativas consideradas

- **HTML o Markdown completo con un sanitizador:** más expresivo, pero agrega una dependencia y una superficie de ataque, y permite romper el diseño. Las necesidades reales son párrafos y enlaces.
- **Sembrar el contenido en la migración (`HasData` o `InsertData`):**
  - con `HasData`, EF administra esas filas, y un cambio posterior al texto semilla generaría una migración que pisa lo que el administrador editó;
  - con `InsertData` no pasa eso, pero el texto quedaría en dos lugares, la migración y el código.
- **Un almacén genérico clave-valor o un CMS externo:** más flexible, pero pierde tipos, longitudes y validación, o agrega un servicio que operar y asegurar para tres bloques de texto.
- **Inyectar el contenido en `index.html` desde el servidor:** evita la petición extra, pero acopla el sitio estático al backend y complica la caché del HTML. Se puede retomar si la medición del LCP lo justifica.
