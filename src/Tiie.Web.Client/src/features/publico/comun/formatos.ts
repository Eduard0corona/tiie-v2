import type { components } from '@/api/esquema';

type EstadoCita = components['schemas']['EstadoCita'];
type RequisitoTexto = components['schemas']['RequisitoTexto'];

const moneda = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });

/** 0 o vacío: «Sin costo». El costo solo se informa: el pago no es en línea. */
export function formatearCosto(costo: number | null | undefined): string {
  return costo ? moneda.format(costo) : 'Sin costo';
}

export const TEXTO_ESTADO_CITA: Record<EstadoCita, string> = {
  Agendada: 'Agendada',
  Atendida: 'Atendida',
  NoAsistio: 'No asistió',
  Cancelada: 'Cancelada',
  Reprogramada: 'Reprogramada',
};

/** Abre el mapa del dispositivo en una pestaña nueva; sin mapas incrustados ni API key (docs/08). */
export function urlComoLlegar(latitud: number | null, longitud: number | null): string | null {
  if (latitud == null || longitud == null) return null;
  return `https://www.google.com/maps/search/?api=1&query=${latitud},${longitud}`;
}

export interface GrupoRequisitos {
  nivel: string;
  textos: string[];
}

/** Agrupa por nivel escolar; los que aplican a todos los niveles van primero como «Para todos». */
export function agruparRequisitos(requisitos: readonly RequisitoTexto[]): GrupoRequisitos[] {
  const grupos = new Map<string, string[]>();
  for (const r of requisitos) {
    const nivel = r.nivelEscolar ?? 'Para todos';
    const textos = grupos.get(nivel) ?? [];
    textos.push(r.texto);
    grupos.set(nivel, textos);
  }
  return [...grupos.entries()]
    .sort(([a], [b]) => (a === 'Para todos' ? -1 : b === 'Para todos' ? 1 : 0))
    .map(([nivel, textos]) => ({ nivel, textos }));
}
