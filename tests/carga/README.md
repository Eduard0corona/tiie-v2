# Pruebas de carga

Escenarios de k6 de [docs/12](../../docs/12-pruebas.md#carga). Se corren contra QA escalado como producción, antes de la puerta de la Fase 2 y antes de cada temporada.

| Script | Perfil | Criterio de éxito |
| --- | --- | --- |
| `apertura.js` | Sube en 1 minuto a 2 veces el pico de reservas por segundo, sobre pocas ofertas, y se mantiene (`K6_MESETA`, por omisión 4 minutos) | Cero cupos sobrevendidos; errores distintos de 409 menores a 1 %; reserva p95 menor a 1 s |
| `sostenido.js` | El pico durante 30 minutos: consultas de disponibilidad y horas; una parte reserva | Disponibilidad p95 menor a 500 ms; CPU de Azure SQL menor a 70 % (en Azure Monitor) |

Un 409 (`SinCupo` o `YaTieneCita`) es la respuesta correcta cuando muchas personas piden el mismo lugar: no cuenta como error.

## Cómo estimar el pico

El pico sale de la consulta de volumen por día del análisis del sistema anterior. Mientras no se tenga, docs/12 pide usar 10 veces el promedio diario de 2025 concentrado en 2 horas:

```text
K6_PICO = 10 × promedio de citas por día en 2025 ÷ 7 200 s
```

Por ejemplo, con 600 citas diarias: 10 × 600 ÷ 7 200 ≈ 0.83 reservas por segundo; la apertura sube a 2 veces ese valor. Redondea hacia arriba.

## Antes de correr contra QA

- **Captcha:** QA debe usar las claves de prueba de Turnstile durante la carga (`Captcha__ClaveSitio` = `1x00000000000000000000AA` y la clave secreta de prueba en Key Vault). Los scripts envían el token de prueba; con claves reales todas las reservas fallarían con `CaptchaInvalido`.
- **Límites por IP:** k6 sale de pocas IP. Sube `Limites__Reserva__Permitidas` y `Limites__PublicoLectura__Permitidas` en la configuración de App Service mientras dura la prueba, y regrésalos al terminar.
- **Escala:** el plan de App Service y el SKU de Azure SQL de QA deben igualar a producción.
- **Datos:** las reservas de carga quedan como citas reales de QA (correo `carga.<curp>@example.com`). Para repetir la apertura, crea ofertas de prueba o limpia sus citas.

## Ejecución

```bash
k6 run -e K6_URL=https://app-tiie-qa.azurewebsites.net -e K6_PICO=1 \
  -e K6_USUARIO=<supervisor de pruebas> -e K6_CONTRASENA=<contraseña> -e K6_TOTP=<clave TOTP> \
  tests/carga/apertura.js

k6 run -e K6_URL=https://app-tiie-qa.azurewebsites.net -e K6_PICO=1 tests/carga/sostenido.js
```

| Variable | Uso |
| --- | --- |
| `K6_URL` | Sitio a probar; por omisión `https://localhost:7180` |
| `K6_PICO` | Reservas (apertura) o visitas (sostenido) por segundo en el pico |
| `K6_OFERTAS` | Ofertas a usar, por ejemplo `1,2`; por omisión las primeras 2 (apertura) o 6 (sostenido) del sitio |
| `K6_MESETA`, `K6_DURACION` | Duración de la meseta de la apertura y de la carga sostenida |
| `K6_PROPORCION_RESERVA` | Parte de las visitas sostenidas que reserva; por omisión 0.2 |
| `K6_USUARIO`, `K6_CONTRASENA`, `K6_TOTP` | Supervisor o Administrador de pruebas. Con ellas, al terminar la apertura se revisa cada cupo de la ventana (`Ocupados` contra `Capacidad`) en `/api/admin/ofertas/{id}/cupos`; sin ellas, solo la disponibilidad pública por día |

Los comandos con secretos van en variables de entorno de la terminal o del pipeline, nunca en archivos del repositorio.
