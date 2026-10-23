import type { components } from '@/api/esquema';

type CitaAgenda = components['schemas']['CitaAgenda'];

export type FiltroAgenda = 'todas' | 'porAtender' | 'atendidas' | 'noAsistio' | 'canceladas';

export const FILTROS_AGENDA: readonly { valor: FiltroAgenda; texto: string }[] = [
  { valor: 'todas', texto: 'Todas' },
  { valor: 'porAtender', texto: 'Por atender' },
  { valor: 'atendidas', texto: 'Atendidas' },
  { valor: 'noAsistio', texto: 'No asistió' },
  { valor: 'canceladas', texto: 'Canceladas' },
];

const PERTENECE: Record<FiltroAgenda, (c: CitaAgenda) => boolean> = {
  todas: () => true,
  porAtender: (c) => c.estado === 'Agendada',
  atendidas: (c) => c.estado === 'Atendida',
  noAsistio: (c) => c.estado === 'NoAsistio',
  // Una reprogramada ya no se atiende en este horario: para el mostrador cuenta igual que una cancelada.
  canceladas: (c) => c.estado === 'Cancelada' || c.estado === 'Reprogramada',
};

export interface ResumenAgenda {
  /** Citas que siguen en este horario: todas menos las canceladas y reprogramadas. */
  programadas: number;
  porFiltro: Record<FiltroAgenda, number>;
  /** Atendidas más No asistió: la asistencia ya quedó registrada. */
  registradas: number;
}

/** Conteos del día para los indicadores y los filtros. Solo presentación: la API decide qué se puede marcar. */
export function resumirAgenda(citas: readonly CitaAgenda[]): ResumenAgenda {
  const porFiltro = Object.fromEntries(
    FILTROS_AGENDA.map((f) => [f.valor, citas.filter(PERTENECE[f.valor]).length]),
  ) as Record<FiltroAgenda, number>;
  return {
    programadas: porFiltro.todas - porFiltro.canceladas,
    porFiltro,
    registradas: porFiltro.atendidas + porFiltro.noAsistio,
  };
}

function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/\p{M}/gu, '').toUpperCase().trim();
}

/** Citas del filtro y que contienen el texto en el nombre, el folio o la CURP (sin acentos ni mayúsculas). */
export function filtrarAgenda(
  citas: readonly CitaAgenda[],
  filtro: FiltroAgenda,
  texto: string,
): CitaAgenda[] {
  const buscado = normalizar(texto);
  return citas.filter(
    (c) =>
      PERTENECE[filtro](c) &&
      (!buscado || [c.nombreCompleto, c.folio, c.curp ?? ''].some((v) => normalizar(v).includes(buscado))),
  );
}

/**
 * La siguiente cita por atender de hoy (la primera Agendada desde la hora actual) y cuántas Agendada ya pasaron
 * su hora sin asistencia registrada. Las horas vienen como 'HH:mm' y la lista, ordenada por hora.
 */
export function seguimientoDeHoy(
  citas: readonly CitaAgenda[],
  ahora: string,
): { siguiente: CitaAgenda | undefined; atrasadas: number } {
  const porAtender = citas.filter((c) => c.estado === 'Agendada');
  return {
    siguiente: porAtender.find((c) => c.hora >= ahora),
    atrasadas: porAtender.filter((c) => c.hora < ahora).length,
  };
}
