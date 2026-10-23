// Carga sostenida (docs/12-pruebas.md#carga): el pico estimado durante 30 minutos. La mayoría consulta
// disponibilidad y horas; una parte reserva. Criterio: disponibilidad p95 menor a 500 ms (la CPU de Azure SQL
// menor a 70 % se revisa en Azure Monitor durante la ejecución).
//
//   k6 run -e K6_URL=https://<qa> -e K6_PICO=8 tests/carga/sostenido.js
import http from 'k6/http';
import { Rate } from 'k6/metrics';
import { LOCAL, ofertasDelEscenario, reservar, URL } from './comun.js';

/** Visitas por segundo en el pico; cada visita consulta disponibilidad. */
const PICO = Number(__ENV.K6_PICO || 5);
/** Parte de las visitas que termina en una reserva. */
const PROPORCION_RESERVA = Number(__ENV.K6_PROPORCION_RESERVA || 0.2);

const errores = new Rate('errores_no_409');

export const options = {
  insecureSkipTLSVerify: LOCAL,
  scenarios: {
    sostenido: {
      executor: 'constant-arrival-rate',
      rate: Math.ceil(PICO),
      timeUnit: '1s',
      duration: __ENV.K6_DURACION || '30m',
      preAllocatedVUs: Math.ceil(PICO) * 2,
      maxVUs: Math.ceil(PICO) * 20,
    },
  },
  thresholds: {
    'http_req_duration{endpoint:disponibilidad}': ['p(95)<500'],
    errores_no_409: ['rate<0.01'],
  },
};

export function setup() {
  return { ofertas: ofertasDelEscenario(Number(__ENV.K6_CUANTAS_OFERTAS || 6)) };
}

export default function (datos) {
  const ofertaId = datos.ofertas[Math.floor(Math.random() * datos.ofertas.length)];
  const disponibilidad = http.get(`${URL}/api/publico/ofertas/${ofertaId}/disponibilidad`, {
    tags: { endpoint: 'disponibilidad' },
  });
  errores.add(disponibilidad.status !== 200);
  if (disponibilidad.status !== 200) return;

  const dias = disponibilidad.json('dias').filter((d) => d.estado === 'Alta' || d.estado === 'Poca');
  if (dias.length === 0) return;
  const dia = dias[Math.floor(Math.random() * dias.length)];
  const cupos = http.get(`${URL}/api/publico/ofertas/${ofertaId}/cupos?fecha=${dia.fecha}`, {
    tags: { endpoint: 'cupos' },
  });
  errores.add(cupos.status !== 200);
  if (cupos.status !== 200 || cupos.json().length === 0 || Math.random() >= PROPORCION_RESERVA) return;

  const lista = cupos.json();
  const reserva = reservar(lista[Math.floor(Math.random() * lista.length)].cupoId);
  errores.add(reserva.status !== 201 && reserva.status !== 409);
}
