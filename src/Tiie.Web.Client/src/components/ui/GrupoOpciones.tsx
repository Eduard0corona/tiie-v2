import { useId, type ReactNode } from 'react';

export interface Opcion {
  valor: number;
  etiqueta: string;
  detalle?: ReactNode;
}

export interface GrupoOpcionesProps {
  leyenda: string;
  opciones: readonly Opcion[];
  seleccion: number | null;
  alElegir: (valor: number) => void;
}

/** Grupo de opciones excluyentes como tarjetas: radios nativos, navegables con teclado y de 44 px o más. */
export function GrupoOpciones({ leyenda, opciones, seleccion, alElegir }: GrupoOpcionesProps) {
  const nombre = useId();
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-2 font-medium text-slate-800">{leyenda}</legend>
      {opciones.map((opcion) => {
        const id = `${nombre}-${opcion.valor}`;
        const elegida = opcion.valor === seleccion;
        return (
          <label
            key={opcion.valor}
            htmlFor={id}
            className={`flex min-h-11 cursor-pointer items-start gap-3 rounded-md border px-3 py-2 ${
              elegida ? 'border-guinda-700 bg-guinda-50 ring-2 ring-guinda-700' : 'border-slate-300 bg-white'
            }`}
          >
            <input
              id={id}
              type="radio"
              name={nombre}
              className="mt-1 size-5 accent-guinda-700"
              checked={elegida}
              onChange={() => alElegir(opcion.valor)}
            />
            <span className="flex flex-col">
              <span className="font-medium">{opcion.etiqueta}</span>
              {opcion.detalle && <span className="text-sm text-slate-600">{opcion.detalle}</span>}
            </span>
          </label>
        );
      })}
    </fieldset>
  );
}
