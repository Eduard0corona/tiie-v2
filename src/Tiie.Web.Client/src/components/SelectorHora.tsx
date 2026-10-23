import type { components } from '@/api/esquema';
import { Icono, type NombreIcono } from '@/components/ui/Icono';

type CupoDisponible = components['schemas']['CupoDisponible'];

export interface SelectorHoraProps {
  leyenda: string;
  cupos: readonly CupoDisponible[];
  seleccion: number | null;
  alElegir: (cupoId: number) => void;
}

interface Turno {
  titulo: string;
  icono: NombreIcono;
  cupos: CupoDisponible[];
}

/** Separa las horas en mañana y tarde para encontrar una más rápido. Las horas vienen como 'HH:mm'. */
export function agruparPorTurno(cupos: readonly CupoDisponible[]): Turno[] {
  const turnos: Turno[] = [
    { titulo: 'Mañana', icono: 'manana', cupos: cupos.filter((c) => c.hora < '12:00') },
    { titulo: 'Tarde', icono: 'tarde', cupos: cupos.filter((c) => c.hora >= '12:00') },
  ];
  return turnos.filter((t) => t.cupos.length > 0);
}

/**
 * Horas con lugar de un día, como un grupo de opciones con cuántos lugares quedan. El radio es invisible y
 * la tarjeta muestra el foco del teclado y el estado elegido con un ícono, no solo con color.
 */
export function SelectorHora({ leyenda, cupos, seleccion, alElegir }: SelectorHoraProps) {
  return (
    <fieldset className="flex min-w-0 flex-col gap-5">
      <legend className="mb-4 text-lg font-bold text-slate-900 first-letter:uppercase">{leyenda}</legend>
      {agruparPorTurno(cupos).map((turno) => (
        <div key={turno.titulo} className="flex flex-col gap-2.5">
          <p className="flex items-center gap-2 text-sm font-semibold text-slate-700">
            <Icono nombre={turno.icono} className="size-5 text-dorado-500" />
            {turno.titulo}
          </p>
          <div className="grid grid-cols-3 gap-2 sm:grid-cols-4">
            {turno.cupos.map((cupo) => {
              const id = `cupo-${cupo.cupoId}`;
              const elegido = cupo.cupoId === seleccion;
              const ultimo = cupo.libres === 1;
              return (
                <label
                  key={cupo.cupoId}
                  htmlFor={id}
                  className={`relative flex min-h-16 cursor-pointer flex-col items-center justify-center rounded-xl border px-2 py-2 text-center transition-colors has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-guinda-700 ${
                    elegido
                      ? 'border-guinda-700 bg-guinda-700 text-white'
                      : 'border-slate-200 bg-white shadow-sm hover:border-guinda-300 hover:bg-guinda-50'
                  }`}
                >
                  <input
                    id={id}
                    type="radio"
                    name="cupo"
                    className="sr-only"
                    checked={elegido}
                    onChange={() => alElegir(cupo.cupoId)}
                  />
                  <span className="text-lg font-bold tabular-nums">{cupo.hora}</span>
                  <span className="sr-only">, </span>
                  <span
                    className={`text-xs font-medium ${
                      elegido ? 'text-white' : ultimo ? 'text-amber-800' : 'text-slate-600'
                    }`}
                  >
                    {ultimo ? 'Último lugar' : `${cupo.libres} lugares`}
                  </span>
                  {elegido && (
                    <span className="absolute top-1.5 right-1.5 grid size-5 place-items-center rounded-full bg-white text-guinda-700">
                      <Icono nombre="check" className="size-3.5" />
                    </span>
                  )}
                </label>
              );
            })}
          </div>
        </div>
      ))}
    </fieldset>
  );
}
