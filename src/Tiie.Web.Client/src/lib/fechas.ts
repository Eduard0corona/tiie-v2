import { TZDate } from '@date-fns/tz';
import { format } from 'date-fns';
import { es } from 'date-fns/locale';

/** Zona de negocio (ADR 0006). Las fechas de la API ya vienen en esta zona. */
export const ZONA = 'America/Mazatlan';

const FECHA_ISO = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Hoy en Sinaloa, como 'yyyy-MM-dd'. */
export function hoyLocal(): string {
  return format(TZDate.tz(ZONA), 'yyyy-MM-dd');
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
