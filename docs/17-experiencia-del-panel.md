# 17 · Experiencia del panel del personal

Diseño de `/admin`: el marco (menú, búsqueda, cuenta), la entrada al panel, la agenda del día, el tablero y el estilo común de todas las pantallas. Usa el mismo sistema de diseño que el sitio público ([16](16-experiencia-de-usuario.md#sistema-de-diseño)). El objetivo es distinto: el ciudadano necesita orientación, el personal necesita rapidez. La API sigue decidiendo el acceso y las reglas ([07](07-autenticacion-y-autorizacion.md)); ocultar un menú o un botón es solo comodidad.

## Problema

| Hallazgo en el diseño anterior | Efecto |
| --- | --- |
| Doce enlaces sueltos en una barra horizontal, sin agrupar | Difícil encontrar la pantalla; en celular, una lista larga |
| Para buscar una cita había que ir a Citas y llenar un formulario | El paso más frecuente del mostrador («¿y su folio?») costaba varios clics |
| La agenda era solo una tabla | No se veía cuántas faltaban, cuál seguía ni cuáles ya pasaron su hora sin registrar asistencia |
| Para cambiar de día había que usar el selector de fecha | Revisar mañana o ayer era lento |
| El tablero eran números sueltos y barras sin riel | Poca jerarquía; el dato principal no destacaba |
| **Defecto:** el botón «Salir» del encabezado se veía blanco sobre blanco desde el rediseño público | Nadie encontraba cómo cerrar sesión |

Este último lo causaba sobrescribir con `className` los colores de `Boton`, que ahora fija su fondo. Regla: los colores de `Boton` no se sobrescriben; si hace falta otro aspecto, se agrega una variante o se usa otro elemento.

## Personas

| Rol | Trabajo diario | Lo que necesita del panel |
| --- | --- | --- |
| **Capturista** de módulo | Atiende el mostrador: recibe a la persona, busca su cita, marca asistencia y registra citas de ventanilla | Encontrar una cita en segundos y marcar con un clic, sin perder de vista la fila |
| **Supervisor** | Revisa cómo va el día y la semana, ajusta horarios y bloqueos, saca reportes | Indicadores claros y acceso directo a la configuración de sus módulos |
| **Administrador** | Usuarios, catálogos, auditoría | Lo mismo, más la administración agrupada aparte |

## Principios

1. **Lo del mostrador primero.** El menú abre con Operación (Agenda, Ventanilla, Citas, Tablero); Configuración y Administración van después.
2. **Buscar desde cualquier pantalla.** La barra superior busca por folio o CURP y lleva a Citas con el texto aplicado; «/» la enfoca, como en otras herramientas de trabajo.
3. **El estado del día a la vista.** La agenda abre con lo que falta, lo que ya pasó su hora y el avance contra la meta de asistencia registrada (95 %, [01](01-vision-y-alcance.md#objetivos)).
4. **Un clic por acción frecuente.** «Atendida» es el botón principal de cada fila; cambiar de día es una flecha.
5. **Densidad cómoda.** Tablas con encabezados discretos, filas de 44 px o más y texto de 14 a 16 px; nada de tarjetas grandes donde basta una fila.
6. **Mismos componentes en todas las pantallas.** Encabezado de página, paneles, tablas, etiquetas de estado y paginación son los mismos en todas las pantallas.

## Marco

- **Escritorio:** barra lateral fija de 272 px con el logotipo, los grupos del menú (cada uno es una lista con nombre: «Operación», «Configuración», «Administración») y, al pie, la persona (iniciales, nombre, rol y número de módulos), «Mi cuenta» y «Salir». La pantalla actual lleva `aria-current="page"`.
- **Celular y tableta:** «Menú» abre la misma barra como panel lateral con fondo oscurecido; se cierra al elegir una pantalla, con Escape o tocando fuera.
- **Barra superior:** búsqueda global (`role="search"`, «Buscar cita por folio o CURP») y, en pantallas anchas, la fecha de hoy en Sinaloa.
- **Entrada al panel:** dos paneles en escritorio. A la izquierda, qué es el panel y el aviso de acceso solo para personal autorizado con verificación en dos pasos; a la derecha, el formulario. En celular, solo el formulario.

## Componentes del panel

En `features/admin/comun/Componentes.tsx`, además de los de [16](16-experiencia-de-usuario.md#componentes):

| Componente | Para qué |
| --- | --- |
| `EncabezadoPagina` | Título (h1 con foco), una frase de para qué sirve la pantalla, acciones a la derecha y, en detalles, «Volver a …» |
| `Panel` y `estiloPanel` | Tarjeta de una sección o de un formulario de filtros |
| `SinResultados` | Lista vacía o búsqueda sin resultados, con qué hacer |
| `EtiquetaEstado` | Estado de la cita en una píldora con punto; el texto siempre dice el estado |
| `TablaDesplazable`, `celdaEncabezado`, `celda` | Tablas con encabezados en versalitas, filas con realce al pasar el puntero y desplazamiento horizontal en celular |
| `Paginacion` | Rango visible y botones con flecha |

`Selector` y `AreaTexto` usan `estiloEntrada`, como `Campo`, para que todos los controles midan lo mismo.

## Agenda del día

- **Encabezado** con «Registrar en ventanilla».
- **Controles:** módulo; día anterior, fecha, día siguiente y «Hoy».
- **Indicadores** (`agenda/resumen.ts`, solo presentación):
  - **Citas:** sin contar canceladas ni reprogramadas.
  - **Por atender:** con «N ya pasaron su hora» o «Siguiente a las HH:mm».
  - **Atendidas** y **No asistió**.
- **Asistencia registrada:** «X de Y» con barra; se pone verde al llegar a la meta.
- **Filtros por estado** como radios con su conteo (Todas, Por atender, Atendidas, No asistió, Canceladas) y búsqueda dentro de la agenda por nombre, folio o CURP, sin importar acentos.
- **Tabla:**
  - Hora, persona, folio, trámite, estado y asistencia.
  - La siguiente cita se resalta con «Siguiente»; las que ya pasaron su hora dicen «Pasó su hora».
  - «Atendida» es verde con ✓ y «No asistió», secundario; los dos conservan sus nombres accesibles «Atendida: {folio}».
- **Actualización:** la agenda de hoy se vuelve a pedir cada minuto y al volver a la pestaña.

## Tablero

Sigue la guía de visualización del proyecto: un número principal por vista, el color solo cuando significa algo y medidores en lugar de gráficas cuando el dato es una proporción contra su límite.

- **Citas de hoy:** total grande con «X de Y con asistencia registrada» (sin canceladas ni reprogramadas) y una tarjeta por estado con ícono y texto.
- **Ocupación de los próximos 7 días:** resumen de la semana y, por día, «ocupados de capacidad» con un medidor de riel guinda claro; los días sin capacidad dicen «Sin servicio».
- **Inasistencia de 30 días:** por módulo, de mayor a menor, con medidor.

Las barras siguen dentro de tablas HTML, como decide [08](08-frontend.md#stack): el valor está escrito junto a cada barra, así que no hace falta una capa de información al pasar el puntero.

## Compatibilidad con las pruebas

Las pruebas E2E del panel (`tests/e2e/pruebas/personal.spec.ts`) y las de componentes dependen de:

- un solo combo «Módulo» en la agenda;
- la agenda como tabla con filas;
- los botones «Atendida: {folio}» y el aviso «Cita {folio}: Atendida.»;
- el título «Agenda del día»;
- el nombre de la persona una sola vez en el marco;
- la navegación «Panel» con «Mi cuenta» dentro;
- el botón «Menú» con `aria-controls="menu-panel"`;
- los filtros de estado como radios: no pueden ser botones que contengan «Atendida» o «No asistió».

Playwright compara por subcadena: ningún otro título debe contener «agenda del día» y ninguna otra etiqueta de la entrada debe contener «usuario» ni «contraseña».

## Backlog

Épica **E23 Experiencia del panel**, en la Fase 3 ([15](15-plan-de-implementacion.md#fase-3--panel-del-personal)).

| Historia | Criterios de aceptación | Prioridad | Estado |
| --- | --- | --- | --- |
| H-3.17 Marco con menú agrupado y búsqueda global | Grupos por tarea y rol; búsqueda por folio o CURP desde cualquier pantalla con «/»; menú lateral en celular que se cierra con Escape | Must | Hecha |
| H-3.18 Agenda para el mostrador | Indicadores del día, siguiente cita, atrasadas, avance contra la meta, filtros, búsqueda, días con flechas y actualización cada minuto | Must | Hecha |
| H-3.19 Tablero legible | Total de hoy destacado, estados con ícono, medidores de ocupación e inasistencia ordenada | Should | Hecha |
| H-3.20 Estilo común de pantallas | Encabezado, paneles, tablas, estados, paginación y controles iguales en todas las pantallas | Should | Hecha |
| H-3.21 Agenda en tarjetas en celular | En menos de 640 px cada cita es una tarjeta con sus acciones a la vista, sin desplazamiento horizontal y sin perder la semántica de lista | Should | Pendiente |
| H-3.22 Atajos en la agenda | Con una fila enfocada, A marca Atendida y N marca No asistió, con confirmación accesible | Could | Pendiente |
| H-3.23 Prueba con personal de módulo | Tres capturistas registran una mañana simulada; tiempo por persona atendida y errores | Must antes de la puerta de la Fase 3 | Pendiente |
