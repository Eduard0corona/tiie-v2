# Política de seguridad

Este sistema guarda datos personales de estudiantes, muchos menores de edad. Si encuentras una vulnerabilidad, repórtala de forma privada.

## Cómo reportar

- **No** abras un issue público ni la comentes en un PR.
- Escribe al contacto de seguridad de la dependencia responsable: `<correo de seguridad por definir antes de la Fase 4>`.
- Incluye la ruta o componente afectado, los pasos para reproducirla y el impacto que observaste.
- No uses datos personales reales en el reporte; usa CURPs ficticias.

## Tiempos de respuesta

| Etapa | Plazo |
| --- | --- |
| Acuse de recibo | 3 días hábiles |
| Evaluación inicial | 7 días hábiles |
| Corrección de severidad crítica o alta | 7 días naturales desde la evaluación |
| Corrección de severidad media | Siguiente despliegue planeado |

## Alcance

- El sitio público, `/api`, el panel `/admin` y los procesos en segundo plano.
- La configuración de los recursos de Azure definida en `infra/`.
- Fuera de alcance: ataques de denegación de servicio, ingeniería social y servicios de terceros (Cloudflare, Microsoft) salvo su configuración en este sistema.

## Si encuentras un secreto en el repositorio

1. Avisa de inmediato al líder técnico.
2. El secreto se **rota primero**: borrarlo del historial no lo vuelve seguro.
3. Después se limpia el historial y se documenta el incidente.

## Prácticas obligatorias

Las reglas de manejo de secretos, datos personales, autorización y dependencias están en [docs/11-seguridad-y-privacidad.md](docs/11-seguridad-y-privacidad.md) y en [CLAUDE.md](CLAUDE.md).
