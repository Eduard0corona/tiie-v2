# Decisiones de arquitectura (ADR)

Cada decisión que cambia la forma del sistema se registra aquí, con su contexto y sus alternativas. Un ADR aceptado no se edita: si la decisión cambia, se escribe uno nuevo que lo declara superado.

| ADR | Decisión | Estado |
| --- | --- | --- |
| [0001](0001-monolito-modular-aspnet-core.md) | Monolito modular en ASP.NET Core 10 | Aceptado |
| [0002](0002-react-spa-en-el-mismo-sitio.md) | React servido por el mismo sitio, con público y `/admin` | Aceptado |
| [0003](0003-aspnet-core-identity-con-cookies-y-mfa.md) | ASP.NET Core Identity con cookies y MFA obligatoria | Aceptado |
| [0004](0004-cupo-materializado.md) | Cupo materializado con actualización condicional | Propuesto |
| [0005](0005-acceso-ciudadano-por-token.md) | Acceso del ciudadano por token, sin cuenta | Propuesto |
| [0006](0006-zona-horaria-america-mazatlan.md) | Zona `America/Mazatlan` y `TimeProvider` | Propuesto |
| [0007](0007-outbox-y-procesos-en-el-sitio.md) | Bandeja de salida en SQL y procesos dentro del sitio | Propuesto |
| [0008](0008-contenido-editable-de-la-portada.md) | Contenido de la portada editable en la base, como texto plano con enlaces | Propuesto |

Los ADR propuestos se ratifican o se ajustan en la primera semana de la Fase 1.

## Plantilla

Copia este bloque en `NNNN-titulo-corto.md`:

```markdown
# NNNN · Título en forma de decisión

- **Estado:** Propuesto | Aceptado | Superado por NNNN
- **Fecha:** AAAA-MM-DD

## Contexto

Qué problema hay y qué fuerzas influyen (requisitos, restricciones, equipo, costos).

## Decisión

Qué se decidió, en una o dos frases, y cómo se aplica.

## Consecuencias

Qué se gana, qué se pierde y qué trabajo nuevo aparece.

## Alternativas consideradas

Cada alternativa con la razón por la que no se eligió.
```
