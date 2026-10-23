import { ZONA, sumarDias } from '@/lib/fechas';

/**
 * Evento de calendario de una cita, para que la persona no la olvide (meta de asistencia, docs/01). Sin datos
 * personales: solo folio, trámite, sede y hora. La hora es local de Sinaloa (UTC-7 todo el año desde 2022).
 */
export interface EventoCita {
  folio: string;
  /** 'yyyy-MM-dd' */
  fecha: string;
  /** 'HH:mm' */
  hora: string;
  tramite: string;
  sede: string;
  direccion: string;
  municipio: string;
}

/** La plantilla no guarda la duración de la atención; media hora reserva el tiempo en el calendario. */
export const DURACION_MINUTOS = 30;

function compacta(fecha: string, minutosDelDia: number): string {
  const dias = Math.floor(minutosDelDia / 1440);
  const resto = minutosDelDia % 1440;
  const dos = (n: number) => String(n).padStart(2, '0');
  return `${sumarDias(fecha, dias).replaceAll('-', '')}T${dos(Math.floor(resto / 60))}${dos(resto % 60)}00`;
}

/** Inicio y fin como 'yyyyMMddTHHmmss' locales. */
export function intervalo(evento: EventoCita): [inicio: string, fin: string] {
  const [h, m] = evento.hora.split(':').map(Number);
  const inicio = (h ?? 0) * 60 + (m ?? 0);
  return [compacta(evento.fecha, inicio), compacta(evento.fecha, inicio + DURACION_MINUTOS)];
}

function titulo(evento: EventoCita) {
  return `Cita TIIE: ${evento.tramite}`;
}

function lugar(evento: EventoCita) {
  return `${evento.sede}, ${evento.direccion}, ${evento.municipio}, Sinaloa`;
}

function descripcion(evento: EventoCita) {
  return `Folio ${evento.folio}. Lleva tu comprobante y los requisitos del trámite. Consulta, cambia o cancela tu cita con el enlace de tu correo.`;
}

/** Enlace para agregar la cita en Google Calendar (Android y web). */
export function urlGoogleCalendar(evento: EventoCita): string {
  const [inicio, fin] = intervalo(evento);
  const parametros = new URLSearchParams({
    action: 'TEMPLATE',
    text: titulo(evento),
    dates: `${inicio}/${fin}`,
    ctz: ZONA,
    location: lugar(evento),
    details: descripcion(evento),
  });
  return `https://calendar.google.com/calendar/render?${parametros.toString()}`;
}

function escapar(texto: string): string {
  return texto.replace(/[\\;,]/g, (c) => `\\${c}`).replace(/\r?\n/g, '\\n');
}

/** RFC 5545: líneas de 75 octetos como máximo; las siguientes empiezan con un espacio. */
function doblar(linea: string): string {
  const codificador = new TextEncoder();
  const partes: string[] = [];
  let actual = '';
  let octetos = 0;
  for (const caracter of linea) {
    const tamano = codificador.encode(caracter).length;
    if (octetos + tamano > (partes.length === 0 ? 75 : 74)) {
      partes.push(actual);
      actual = '';
      octetos = 0;
    }
    actual += caracter;
    octetos += tamano;
  }
  partes.push(actual);
  return partes.join('\r\n ');
}

/** Archivo .ics (Apple, Outlook y demás). `ahora` es el instante de creación, en milisegundos UTC. */
export function crearIcs(evento: EventoCita, ahora: number): string {
  const [inicio, fin] = intervalo(evento);
  const sello = new Date(ahora)
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}/, '');
  const lineas = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Gobierno del Estado de Sinaloa//Citas TIIE//ES',
    'CALSCALE:GREGORIAN',
    'METHOD:PUBLISH',
    'BEGIN:VTIMEZONE',
    `TZID:${ZONA}`,
    'BEGIN:STANDARD',
    'DTSTART:19700101T000000',
    'TZOFFSETFROM:-0700',
    'TZOFFSETTO:-0700',
    'TZNAME:MST',
    'END:STANDARD',
    'END:VTIMEZONE',
    'BEGIN:VEVENT',
    `UID:${evento.folio}@citas-tiie`,
    `DTSTAMP:${sello}`,
    `DTSTART;TZID=${ZONA}:${inicio}`,
    `DTEND;TZID=${ZONA}:${fin}`,
    `SUMMARY:${escapar(titulo(evento))}`,
    `LOCATION:${escapar(lugar(evento))}`,
    `DESCRIPTION:${escapar(descripcion(evento))}`,
    'BEGIN:VALARM',
    'ACTION:DISPLAY',
    'TRIGGER:-P1D',
    `DESCRIPTION:${escapar(`Mañana es tu cita TIIE a las ${evento.hora}`)}`,
    'END:VALARM',
    'END:VEVENT',
    'END:VCALENDAR',
  ];
  return `${lineas.map(doblar).join('\r\n')}\r\n`;
}
