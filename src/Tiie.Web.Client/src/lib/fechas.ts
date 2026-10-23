import { TZDate } from '@date-fns/tz';
import { addDays, differenceInCalendarDays, format } from 'date-fns';
import { es } from 'date-fns/locale';

/** Zona de negocio (ADR 0006). Las fechas de la API ya vienen en esta zona. */
export const ZONA = 'America/Mazatlan';

const FECHA_ISO = /^(\d{4})-(\d{2})-(\d{2})$/;

// «Ahora» se lee con Intl y la zona explícita, sin TZDate: con el reloj simulado de las pruebas, TZDate.tz llegó
// a devolver la hora de la máquina en lugar de la de Sinaloa.
const RELOJ_SINALOA = new Intl.DateTimeFormat('en-CA', {
  timeZone: ZONA,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

function ahoraEnSinaloa(): { fecha: string; hora: string } {
  const partes = new Map(RELOJ_SINALOA.formatToParts(Date.now()).map((p) => [p.type, p.value]));
  const parte = (tipo: Intl.DateTimeFormatPartTypes) => partes.get(tipo) ?? '';
  return {
    fecha: `${parte('year')}-${parte('month')}-${parte('day')}`,
    hora: `${parte('hour')}:${parte('minute')}`,
  };
}

/** Hoy en Sinaloa, como 'yyyy-MM-dd'. */
export function hoyLocal(): string {
  return ahoraEnSinaloa().fecha;
}

/** Interpreta 'yyyy-MM-dd' como un día de Sinaloa, sin pasar por medianoche UTC. */
function diaLocal(fechaIso: string): TZDate {
  const partes = FECHA_ISO.exec(fechaIso);
  if (!partes) throw new Error(`Fecha con formato inválido: ${fechaIso}`);
  const [, anio, mes, dia] = partes.map(Number) as [number, number, number, number];
  return new TZDate(anio, mes - 1, dia, ZONA);
}

/** '2026-11-12' → 'jueves 12 de noviembre de 2026'. */
export function formatearFechaLarga(fechaIso: string): string {
  return format(diaLocal(fechaIso), "EEEE d 'de' MMMM 'de' yyyy", { locale: es });
}

/** '2026-11-12' → '12/11/2026'. */
export function formatearFechaCorta(fechaIso: string): string {
  return format(diaLocal(fechaIso), 'dd/MM/yyyy');
}

/** Instante UTC de la API ('2026-11-03T17:04:11Z') → hora de Sinaloa, por ejemplo '10:04'. */
export function formatearHoraDeInstante(instanteIso: string): string {
  return format(new TZDate(Date.parse(instanteIso), ZONA), 'HH:mm');
}

/** Indica si el texto es una fecha 'yyyy-MM-dd' que existe en el calendario. */
export function esFechaIso(valor: string | null | undefined): valor is string {
  const partes = valor ? FECHA_ISO.exec(valor) : null;
  if (!partes) return false;
  const [, anio, mes, dia] = partes.map(Number) as [number, number, number, number];
  const fecha = new Date(anio, mes - 1, dia);
  return fecha.getFullYear() === anio && fecha.getMonth() === mes - 1 && fecha.getDate() === dia;
}

/**
 * Día del calendario como Date a medianoche del navegador, para componentes que trabajan con Date
 * (react-day-picker). Se construye por partes: nunca pasa por UTC. Vuelve a texto con `isoDeCalendario`.
 */
export function fechaCalendario(fechaIso: string): Date {
  const partes = FECHA_ISO.exec(fechaIso);
  if (!partes) throw new Error(`Fecha con formato inválido: ${fechaIso}`);
  const [, anio, mes, dia] = partes.map(Number) as [number, number, number, number];
  return new Date(anio, mes - 1, dia);
}

/** Date de un componente de calendario → 'yyyy-MM-dd', con el día que se ve en pantalla. */
export function isoDeCalendario(fecha: Date): string {
  return format(fecha, 'yyyy-MM-dd');
}

/** '2026-11-12' → 'jueves 12 de noviembre'. */
export function formatearDiaSemana(fechaIso: string): string {
  return format(diaLocal(fechaIso), "EEEE d 'de' MMMM", { locale: es });
}

/** '2026-11-12' → ['jue', '12', 'nov'], para tarjetas de día compactas. */
export function formatearDiaCorto(fechaIso: string): [semana: string, dia: string, mes: string] {
  const dia = diaLocal(fechaIso);
  return [format(dia, 'EEE', { locale: es }), format(dia, 'd'), format(dia, 'MMM', { locale: es })];
}

/** Días de calendario de `desde` a `hasta` ('yyyy-MM-dd'); negativo si `hasta` es anterior. */
export function diasEntre(desde: string, hasta: string): number {
  return differenceInCalendarDays(diaLocal(hasta), diaLocal(desde));
}

/** Suma días a una fecha 'yyyy-MM-dd' sin pasar por UTC; acepta negativos. */
export function sumarDias(fechaIso: string, dias: number): string {
  return format(addDays(diaLocal(fechaIso), dias), 'yyyy-MM-dd');
}

/** Hora actual en Sinaloa, como 'HH:mm'. */
export function horaLocal(): string {
  return ahoraEnSinaloa().hora;
}

/** Instante UTC de la API → '12/11/2026 10:04' en hora de Sinaloa. */
export function formatearInstante(instanteIso: string): string {
  return format(new TZDate(Date.parse(instanteIso), ZONA), 'dd/MM/yyyy HH:mm');
}

/** Primer día del mes de una fecha 'yyyy-MM-dd'. */
export function inicioDeMes(fechaIso: string): string {
  return `${fechaIso.slice(0, 7)}-01`;
}
