# Cómo contribuir

Este repositorio usa ramas cortas sobre `main`, PR pequeños y CI obligatorio. Las reglas técnicas que no se rompen están en [CLAUDE.md](CLAUDE.md); aquí está el flujo de trabajo.

## Flujo

1. Toma una historia del plan ([docs/15](docs/15-plan-de-implementacion.md)) o un issue.
2. Crea una rama desde `main`: `tipo/H-x.y-descripcion-corta`, por ejemplo `feat/H-2.5-reserva-atomica` o `fix/H-3.7-asistencia-futura`.
3. Haz cambios pequeños; un PR debería poder revisarse en menos de una hora.
4. Abre el PR con la plantilla y enlaza la historia.
5. Espera CI en verde y las aprobaciones.
6. Integra con *squash merge*; el título del PR es el mensaje del commit.

`main` está protegida: nadie hace push directo y todo pasa por PR.

## Aprobaciones

| Cambio | Aprobaciones |
| --- | --- |
| General | 1 |
| Autenticación, autorización, CSRF, tokens o cabeceras | 2, una del líder técnico |
| Reglas `RN-xx` o la reserva atómica | 2 |
| Migración que borra o renombra columnas o tablas | 2 |
| Infraestructura (`infra/`) o pipeline | 2 |

## Commits y títulos de PR

Conventional Commits en español:

```text
feat(reservas): reserva atómica con idempotencia
fix(agenda): no permitir asistencia en citas futuras
test(cupos): concurrencia de 100 reservas por el último lugar
docs(api): documentar códigos de error de Mi cita
refactor(dominio): extraer CalculadoraDisponibilidad
chore(deps): actualizar EF Core
```

## Antes de abrir un PR

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
cd src/Tiie.Web.Client && npm run lint && npm run typecheck && npm test
npm run api:tipos   # si cambiaste un endpoint; incluye el archivo generado en el PR
```

## Qué revisar en un PR

- [ ] ¿Alguna regla de negocio quedó en el endpoint o en React en lugar de en el dominio?
- [ ] ¿Se usa `TimeProvider` y `RelojAgenda`, y no el reloj del sistema?
- [ ] ¿Los cambios a `Cupo.Ocupados` son un `UPDATE` condicional dentro de la transacción?
- [ ] ¿Los endpoints nuevos tienen política, verifican el módulo y están en la matriz de permisos?
- [ ] ¿Algún log, error o respuesta incluye datos personales sin enmascarar?
- [ ] ¿Las consultas usan `AsNoTracking` y proyecciones, sin N+1?
- [ ] ¿La migración es compatible con la versión anterior del código?
- [ ] ¿Hay pruebas que fallarían sin el cambio?
- [ ] ¿La interfaz funciona con teclado y en celular?
- [ ] ¿Se actualizó la documentación afectada?

## Migraciones

- Una migración por PR, con nombre descriptivo en español.
- Compatibles hacia atrás: primero se agrega, el código adopta lo nuevo y lo viejo se elimina en un PR posterior. Durante el intercambio de ranuras conviven ambas versiones del código.
- Nunca se edita una migración que ya se aplicó en QA.
- Los datos semilla de producción (roles, niveles escolares) van en migraciones; los de ejemplo, en el sembrador de desarrollo.

## Pruebas

- Nombre en español que describe el comportamiento; si cubre una regla, empieza con su ID: `RN13_el_ciudadano_no_puede_cancelar_el_dia_de_la_cita`.
- Las que usan contenedores viven en `Tiie.Integracion.Tests`, que declara `[assembly: Trait("Category", "Integracion")]`; `dotnet test --filter-not-trait "Category=Integracion"` las omite.
- Una corrección de error llega con la prueba que lo reproduce.

## Documentación y ADR

- Si cambias una regla, un endpoint, el esquema o la configuración, actualiza el documento de `docs/` correspondiente en el mismo PR.
- Si cambias una decisión de arquitectura, escribe un ADR nuevo con la plantilla de [docs/adr](docs/adr/README.md).

## Dependencias

- Justifica en el PR cualquier paquete nuevo de NuGet o npm: qué resuelve, licencia y mantenimiento.
- Las actualizaciones automáticas (Dependabot o Renovate) se revisan cada semana; las de seguridad, el mismo día.

## Definición de terminado

Está en [docs/15-plan-de-implementacion.md](docs/15-plan-de-implementacion.md#definición-de-terminado).
