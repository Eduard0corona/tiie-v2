import { es } from 'date-fns/locale';
import { format } from 'date-fns';
import type { CSSProperties } from 'react';
import { DayPicker, type Matcher } from 'react-day-picker';
import 'react-day-picker/style.css';
import type { components } from '@/api/esquema';
import { fechaCalendario, formatearDiaSemana, hoyLocal, isoDeCalendario } from '@/lib/fechas';

type DiaDisponible = components['schemas']['DiaDisponible'];
type EstadoDia = components['schemas']['EstadoDia'];

/** Texto del estado de cada día: el lector de pantalla lo anuncia y no depende solo del color. */
export const TEXTO_ESTADO_DIA: Record<EstadoDia, string> = {
  Alta: 'alta disponibilidad',
  Poca: 'poca disponibilidad',
  Llena: 'sin lugares',
  Cerrado: 'cerrado',
  SinServicio: 'sin servicio',
};

const SELECCIONABLES: readonly EstadoDia[] = ['Alta', 'Poca'];

export interface CalendarioDisponibilidadProps {
  dias: readonly DiaDisponible[];
  desde: string;
  hasta: string;
  seleccion: string | null;
  alElegir: (fechaIso: string) => void;
}

const estilo = {
  '--rdp-accent-color': '#7b1e3a',
  '--rdp-accent-background-color': '#f5e1e6',
  '--rdp-day-height': '44px',
  '--rdp-day-width': '44px',
  '--rdp-day_button-height': '42px',
  '--rdp-day_button-width': '42px',
} as CSSProperties;

/**
 * Calendario de la ventana de reserva (RF-02). Los días Llena, Cerrado y SinServicio no se pueden elegir;
 * cada día anuncia su estado («jueves 12 de noviembre, poca disponibilidad»).
 */
export function CalendarioDisponibilidad({
  dias,
  desde,
  hasta,
  seleccion,
  alElegir,
}: CalendarioDisponibilidadProps) {
  const porFecha = new Map(dias.map((d) => [d.fecha, d]));
  const fechasCon = (estados: readonly EstadoDia[]) =>
    dias.filter((d) => estados.includes(d.estado)).map((d) => fechaCalendario(d.fecha));
  const noElegible: Matcher = (fecha) => {
    const dia = porFecha.get(isoDeCalendario(fecha));
    return !dia || !SELECCIONABLES.includes(dia.estado);
  };
  const cerrados = dias.filter((d) => d.estado === 'Cerrado');

  return (
    <div className="flex flex-col gap-4">
      <div className="overflow-x-auto rounded-md border border-slate-200 bg-white p-2" style={estilo}>
        <DayPicker
          mode="single"
          locale={es}
          required={false}
          selected={seleccion ? fechaCalendario(seleccion) : undefined}
          onSelect={(fecha) => fecha && alElegir(isoDeCalendario(fecha))}
          defaultMonth={fechaCalendario(seleccion ?? desde)}
          startMonth={fechaCalendario(desde)}
          endMonth={fechaCalendario(hasta)}
          today={fechaCalendario(hoyLocal())}
          showOutsideDays={false}
          disabled={[{ before: fechaCalendario(desde) }, { after: fechaCalendario(hasta) }, noElegible]}
          modifiers={{ poca: fechasCon(['Poca']), alta: fechasCon(['Alta']) }}
          modifiersClassNames={{
            alta: 'font-semibold',
            poca: 'underline decoration-dotted decoration-2 underline-offset-4',
            disabled: 'line-through',
          }}
          labels={{
            labelDayButton: (fecha, modificadores) => {
              const iso = isoDeCalendario(fecha);
              const dia = porFecha.get(iso);
              const estado = dia ? TEXTO_ESTADO_DIA[dia.estado] : 'fuera del periodo de reserva';
              const motivo = dia?.estado === 'Cerrado' && dia.motivo ? `: ${dia.motivo}` : '';
              return `${formatearDiaSemana(iso)}, ${estado}${motivo}${modificadores.selected ? ', elegido' : ''}`;
            },
            labelPrevious: () => 'Mes anterior',
            labelNext: () => 'Mes siguiente',
            labelGrid: (fecha) => format(fecha, "MMMM 'de' yyyy", { locale: es }),
          }}
        />
      </div>
      <ul className="flex flex-col gap-1 text-sm text-slate-700" aria-label="Cómo leer el calendario">
        <li>
          <span className="font-semibold">Número en negritas:</span> hay lugares.
        </li>
        <li>
          <span className="underline decoration-dotted decoration-2 underline-offset-4">
            Subrayado punteado
          </span>
          : quedan pocos lugares.
        </li>
        <li>
          <span className="line-through">Tachado</span>: sin lugares, cerrado o sin servicio.
        </li>
      </ul>
      {cerrados.length > 0 && (
        <div className="text-sm text-slate-700">
          <p className="font-medium">Días cerrados</p>
          <ul className="list-disc pl-5">
            {cerrados.map((d) => (
              <li key={d.fecha}>
                {formatearDiaSemana(d.fecha)}
                {d.motivo ? `: ${d.motivo}` : ''}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
