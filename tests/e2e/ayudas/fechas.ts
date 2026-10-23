// Aritmética de fechas 'yyyy-MM-dd' sin zona horaria. «Hoy» sale de la API (desde − 1 día), no del reloj del equipo.

const DIAS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const MESES = [
  'enero',
  'febrero',
  'marzo',
  'abril',
  'mayo',
  'junio',
  'julio',
  'agosto',
  'septiembre',
  'octubre',
  'noviembre',
  'diciembre',
];

function partes(iso: string): [number, number, number] {
  const coincide = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!coincide) throw new Error(`Fecha inválida: ${iso}`);
  return [Number(coincide[1]), Number(coincide[2]), Number(coincide[3])];
}

function utc(iso: string): Date {
  const [a, m, d] = partes(iso);
  return new Date(Date.UTC(a, m - 1, d));
}

export function sumarDias(iso: string, dias: number): string {
  const fecha = utc(iso);
  fecha.setUTCDate(fecha.getUTCDate() + dias);
  return fecha.toISOString().slice(0, 10);
}

/** 0 = domingo … 6 = sábado, como `DiaSemana` en la plantilla. */
export function diaSemana(iso: string): number {
  return utc(iso).getUTCDay();
}

/** «viernes 23 de octubre»: inicio de la etiqueta accesible de cada día del calendario. */
export function etiquetaDia(iso: string): string {
  const [, m, d] = partes(iso);
  return `${DIAS[diaSemana(iso)]} ${d} de ${MESES[m - 1]}`;
}

/** «viernes 23 de octubre de 2026», como `formatearFechaLarga` de la SPA. */
export function fechaLarga(iso: string): string {
  const [a] = partes(iso);
  return `${etiquetaDia(iso)} de ${a}`;
}

/** «23/10/2026», como `formatearFechaCorta` de la SPA. */
export function fechaCorta(iso: string): string {
  const [a, m, d] = partes(iso);
  return `${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}/${a}`;
}
