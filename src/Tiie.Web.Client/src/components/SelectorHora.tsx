import type { components } from '@/api/esquema';

type CupoDisponible = components['schemas']['CupoDisponible'];

export interface SelectorHoraProps {
  leyenda: string;
  cupos: readonly CupoDisponible[];
  seleccion: number | null;
  alElegir: (cupoId: number) => void;
}

/** Horas con lugar de un día, como un grupo de opciones con cuántos lugares quedan. */
export function SelectorHora({ leyenda, cupos, seleccion, alElegir }: SelectorHoraProps) {
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-2 font-medium text-slate-800">{leyenda}</legend>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {cupos.map((cupo) => {
          const id = `cupo-${cupo.cupoId}`;
          const elegido = cupo.cupoId === seleccion;
          return (
            <label
              key={cupo.cupoId}
              htmlFor={id}
              className={`flex min-h-11 cursor-pointer flex-col rounded-md border px-3 py-2 ${
                elegido
                  ? 'border-guinda-700 bg-guinda-50 ring-2 ring-guinda-700'
                  : 'border-slate-300 bg-white'
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
              <span className="text-lg font-semibold">{cupo.hora}</span>
              <span className="text-sm text-slate-600">
                {cupo.libres === 1 ? '1 lugar' : `${cupo.libres} lugares`}
              </span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
