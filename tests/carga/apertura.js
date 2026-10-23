// Apertura de temporada (docs/12-pruebas.md#carga): en 1 minuto sube a 2 veces el pico estimado de reservas
// por segundo, sobre pocas ofertas, y se mantiene ahí. Criterios: cero sobreventa, errores distintos de 409
// menores a 1 % y reserva p95 menor a 1 s.
//
//   k6 run -e K6_URL=https://<qa> -e K6_PICO=8 tests/carga/apertura.js
import { check } from 'k6';
import http from 'k6/http';
import { Counter, Rate } from 'k6/metrics';
import { cuposSobrevendidos, entrarAlPanel, LOCAL, ofertasDelEscenario, reservar, URL } from './comun.js';

/** Reservas por segundo del pico estimado (README: cómo estimarlo). */
const PICO = Number(__ENV.K6_PICO || 5);
const MESETA = __ENV.K6_MESETA || '4m';
const OBJETIVO = Math.ceil(2 * PICO);

const erroresNo409 = new Rate('errores_no_409');
const creadas = new Counter('reservas_creadas');
const conflictos = new Counter('reservas_409');
const sobrevendidos = new Counter('cupos_sobrevendidos');

export const options = {
  insecureSkipTLSVerify: LOCAL,
  scenarios: {
    apertura: {
      executor: 'ramping-arrival-rate',
      startRate: 0,
      timeUnit: '1s',
      preAllocatedVUs: OBJETIVO * 2,
      maxVUs: OBJETIVO * 20,
      stages: [
        { target: OBJETIVO, duration: '1m' },
        { target: OBJETIVO, duration: MESETA },
      ],
    },
  },
  thresholds: {
    errores_no_409: ['rate<0.01'],
    'http_req_duration{endpoint:reserva}': ['p(95)<1000'],
    cupos_sobrevendidos: ['count==0'],
  },
};

export function setup() {
  // «Pocas ofertas»: la demanda se concentra como en la apertura real.
  return { ofertas: ofertasDelEscenario(Number(__ENV.K6_CUANTAS_OFERTAS || 2)) };
}

export default function (datos) {
  const ofertaId = datos.ofertas[Math.floor(Math.random() * datos.ofertas.length)];
  const disponibilidad = http.get(`${URL}/api/publico/ofertas/${ofertaId}/disponibilidad`, {
    tags: { endpoint: 'disponibilidad' },
  });
  erroresNo409.add(disponibilidad.status !== 200);
  if (disponibilidad.status !== 200) return;

  // La gente pide los primeros días con lugar: ahí se concentra la contienda.
  const dias = disponibilidad.json('dias').filter((d) => d.estado === 'Alta' || d.estado === 'Poca');
  if (dias.length === 0) return;
  const dia = dias[Math.floor(Math.random() * Math.min(dias.length, 3))];
  const cupos = http.get(`${URL}/api/publico/ofertas/${ofertaId}/cupos?fecha=${dia.fecha}`, {
    tags: { endpoint: 'cupos' },
  });
  erroresNo409.add(cupos.status !== 200);
  if (cupos.status !== 200 || cupos.json().length === 0) return;
  const lista = cupos.json();
  const cupo = lista[Math.floor(Math.random() * lista.length)];

  const reserva = reservar(cupo.cupoId);
  erroresNo409.add(reserva.status !== 201 && reserva.status !== 409);
  if (reserva.status === 201) creadas.add(1);
  else if (reserva.status === 409) conflictos.add(1);
  check(reserva, { 'reserva 201 o 409': (r) => r.status === 201 || r.status === 409 });
}

export function teardown(datos) {
  const conPanel = entrarAlPanel();
  const encontrados = cuposSobrevendidos(datos.ofertas, conPanel);
  sobrevendidos.add(encontrados.length);
  for (const e of encontrados) console.error(`Sobreventa: ${e}`);
  if (!conPanel) console.warn('Sin K6_USUARIO/K6_CONTRASENA/K6_TOTP: la sobreventa se revisó solo con la disponibilidad pública.');
}
