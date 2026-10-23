import { useId, type ReactNode } from 'react';
import { Icono, type NombreIcono } from './Icono';

export interface Opcion {
  valor: number;
  etiqueta: string;
  detalle?: ReactNode;
  /** A la derecha de la etiqueta, por ejemplo el costo. */
  insignia?: ReactNode;
  icono?: NombreIcono;
}

export interface GrupoOpcionesProps {
  leyenda: string;
  /** Número de la elección dentro del paso («1 Municipio»); solo visual. */
  numero?: number;
  descripcion?: string;
  opciones: readonly Opcion[];
  seleccion: number | null;
  alElegir: (valor: number) => void;
  /** `chips` para listas cortas de una palabra (municipios); `tarjetas` para opciones con detalle. */
  variante?: 'tarjetas' | 'chips';
}

/**
 * Grupo de opciones excluyentes: radios nativos, navegables con teclado y de 44 px o más. El radio se ve
 * (no está oculto) para que el estado elegido no dependa solo del borde de la tarjeta.
 */
export function GrupoOpciones({
  leyenda,
  numero,
  descripcion,
  opciones,
  seleccion,
  alElegir,
  variante = 'tarjetas',
}: GrupoOpcionesProps) {
  const nombre = useId();
  const chips = variante === 'chips';
  return (
    <fieldset className="flex min-w-0 flex-col gap-3">
      <legend className="mb-3 flex items-center gap-2.5 text-lg font-bold text-slate-900">
        {numero !== undefined && (
          <span
            aria-hidden="true"
            className="grid size-7 place-items-center rounded-full bg-guinda-100 text-sm font-bold text-guinda-800"
          >
            {numero}
          </span>
        )}
        {leyenda}
      </legend>
      {descripcion && <p className="-mt-2 text-sm text-slate-600">{descripcion}</p>}
      <div className={chips ? 'flex flex-wrap gap-2' : 'grid gap-3 sm:grid-cols-2'}>
        {opciones.map((opcion) => {
          const id = `${nombre}-${opcion.valor}`;
          const elegida = opcion.valor === seleccion;
          return (
            <label
              key={opcion.valor}
              htmlFor={id}
              className={`flex cursor-pointer gap-3 border bg-white transition-all ${
                chips
                  ? 'min-h-11 items-center rounded-full py-2 pr-4 pl-3 font-medium'
                  : 'min-h-11 items-start rounded-xl p-4'
              } ${
                elegida
                  ? 'border-guinda-600 bg-guinda-50 ring-1 ring-guinda-600'
                  : 'border-slate-200 shadow-sm hover:border-slate-400'
              }`}
            >
              <input
                id={id}
                type="radio"
                name={nombre}
                className={`size-5 shrink-0 cursor-pointer appearance-none rounded-full border-2 border-slate-400 bg-white transition-all checked:border-[6px] checked:border-guinda-700 ${
                  chips ? '' : 'mt-0.5'
                }`}
                checked={elegida}
                onChange={() => alElegir(opcion.valor)}
              />
              {chips ? (
                <span>{opcion.etiqueta}</span>
              ) : (
                <span className="flex min-w-0 flex-1 flex-col gap-1">
                  <span className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
                    <span className="flex items-center gap-2 font-semibold text-slate-900">
                      {opcion.icono && <Icono nombre={opcion.icono} className="size-5 text-guinda-700" />}
                      {opcion.etiqueta}
                    </span>
                    {opcion.insignia}
                  </span>
                  {opcion.detalle && <span className="text-sm text-slate-600">{opcion.detalle}</span>}
                </span>
              )}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
