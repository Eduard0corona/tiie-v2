import { es } from 'date-fns/locale';
import { format } from 'date-fns';
import { useEffect, useRef } from 'react';
import { DayPicker, type DayButtonProps, type Matcher } from 'react-day-picker';
import 'react-day-picker/style.css';
import type { components } from '@/api/esquema';
import { Icono } from '@/components/ui/Icono';
import {
  fechaCalendario,
  formatearDiaCorto,
  formatearDiaSemana,
  hoyLocal,
  isoDeCalendario,
} from '@/lib/fechas';

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

/** Cuántos días se ofrecen como atajo en «Lo más pronto». */
const DIAS_SUGERIDOS = 3;

export interface CalendarioDisponibilidadProps {
  dias: readonly DiaDisponible[];
  desde: string;
  hasta: string;
  seleccion: string | null;
  alElegir: (fechaIso: string) => void;
  /** Nivel de los subtítulos: 3 cuando el calendario va dentro de una sección con su propio h2. */
  nivelTitulos?: 2 | 3;
}

/** Número del día con un punto que repite su disponibilidad: lleno = hay lugares; aro = quedan pocos. */
function BotonDia({ day, modifiers, children, ...props }: DayButtonProps) {
  const ref = useRef<HTMLButtonElement>(null);
  // Igual que el botón de react-day-picker: el foco sigue al día enfocado con las flechas del teclado.
  useEffect(() => {
    if (modifiers.focused) ref.current?.focus();
  }, [modifiers.focused]);
  const punto = modifiers.disabled
    ? null
    : modifiers.poca
      ? 'punto-poca'
      : modifiers.alta
        ? 'punto-alta'
        : null;
  return (
    <button ref={ref} {...props} data-fecha={day.isoDate}>
      <span>{children}</span>
      {punto && <span aria-hidden="true" className={`punto-disponibilidad ${punto}`} />}
    </button>
  );
}

/**
 * Calendario de la ventana de reserva (RF-02). Los días Llena, Cerrado y SinServicio no se pueden elegir;
 * cada día anuncia su estado («jueves 12 de noviembre, poca disponibilidad»). Arriba ofrece los días más
 * próximos con lugar como atajo: la mayoría de las personas quiere «lo antes posible».
 */
export function CalendarioDisponibilidad({
  dias,
  desde,
  hasta,
  seleccion,
  alElegir,
  nivelTitulos = 2,
}: CalendarioDisponibilidadProps) {
  const Titulo = nivelTitulos === 3 ? 'h3' : 'h2';
  const porFecha = new Map(dias.map((d) => [d.fecha, d]));
  const fechasCon = (estados: readonly EstadoDia[]) =>
    dias.filter((d) => estados.includes(d.estado)).map((d) => fechaCalendario(d.fecha));
  const noElegible: Matcher = (fecha) => {
    const dia = porFecha.get(isoDeCalendario(fecha));
    return !dia || !SELECCIONABLES.includes(dia.estado);
  };
  const cerrados = dias.filter((d) => d.estado === 'Cerrado');
  const sugeridos = dias.filter((d) => SELECCIONABLES.includes(d.estado)).slice(0, DIAS_SUGERIDOS);

  return (
    <div className="flex flex-col gap-6">
      {sugeridos.length > 0 && (
        <div className="flex flex-col gap-3">
          <Titulo className="flex items-center gap-2 font-bold text-slate-900">
            <Icono nombre="rayo" className="size-5 text-dorado-500" />
            Lo más pronto
          </Titulo>
          <ul className="grid grid-cols-3 gap-2 sm:gap-3">
            {sugeridos.map((dia) => {
              const elegido = dia.fecha === seleccion;
              const [semana, numero, mes] = formatearDiaCorto(dia.fecha);
              return (
                <li key={dia.fecha}>
                  <button
                    type="button"
                    onClick={() => alElegir(dia.fecha)}
                    aria-label={`${formatearDiaSemana(dia.fecha)} (${TEXTO_ESTADO_DIA[dia.estado]})`}
                    aria-pressed={elegido}
                    className={`flex w-full flex-col items-center rounded-xl border px-2 py-3 text-center shadow-sm transition-colors ${
                      elegido
                        ? 'border-guinda-700 bg-guinda-700 text-white'
                        : 'border-slate-200 bg-white text-slate-900 hover:border-guinda-300 hover:bg-guinda-50'
                    }`}
                  >
                    <span className="text-sm font-medium capitalize">{semana}</span>
                    <span className="text-2xl leading-tight font-bold">{numero}</span>
                    <span className="text-sm capitalize">{mes}</span>
                    <span
                      className={`mt-1 inline-flex items-center gap-1 text-xs font-semibold ${
                        elegido ? 'text-white' : dia.estado === 'Poca' ? 'text-amber-800' : 'text-emerald-800'
                      }`}
                    >
                      <span
                        aria-hidden="true"
                        className={`punto-disponibilidad ${dia.estado === 'Poca' ? 'punto-poca' : 'punto-alta'}`}
                      />
                      {dia.estado === 'Poca' ? 'Quedan pocos' : 'Hay lugares'}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <div className="flex flex-col gap-3">
        <Titulo className="font-bold text-slate-900">O elige otro día</Titulo>
        <div className="calendario rounded-2xl border border-slate-200 bg-white p-3 shadow-tarjeta sm:p-5">
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
            components={{ DayButton: BotonDia }}
            modifiers={{
              poca: fechasCon(['Poca']),
              alta: fechasCon(['Alta']),
              sinLugar: fechasCon(['Llena', 'Cerrado']),
            }}
            modifiersClassNames={{ alta: 'dia-alta', poca: 'dia-poca', sinLugar: 'dia-sin-lugar' }}
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
        <ul
          className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-slate-700"
          aria-label="Cómo leer el calendario"
        >
          <li className="flex items-center gap-2">
            <span aria-hidden="true" className="punto-disponibilidad punto-alta" />
            Hay lugares
          </li>
          <li className="flex items-center gap-2">
            <span aria-hidden="true" className="punto-disponibilidad punto-poca" />
            Quedan pocos lugares
          </li>
          <li className="flex items-center gap-2">
            <span aria-hidden="true" className="font-semibold text-slate-500 line-through">
              12
            </span>
            Sin lugares o cerrado
          </li>
        </ul>
      </div>

      {cerrados.length > 0 && (
        <div className="rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-700">
          <p className="font-semibold text-slate-900">Días cerrados</p>
          <ul className="mt-1 flex flex-col gap-0.5">
            {cerrados.map((d) => (
              <li key={d.fecha}>
                <span className="first-letter:uppercase">{formatearDiaSemana(d.fecha)}</span>
                {d.motivo ? `: ${d.motivo}` : ''}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
