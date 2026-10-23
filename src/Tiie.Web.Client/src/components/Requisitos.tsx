import type { components } from '@/api/esquema';
import { Icono } from '@/components/ui/Icono';
import { agruparRequisitos } from '@/features/publico/comun/formatos';

type RequisitoTexto = components['schemas']['RequisitoTexto'];

/**
 * Requisitos agrupados por nivel escolar, como lista para revisar antes de salir de casa. Son texto plano:
 * los saltos de línea se respetan.
 */
export function Requisitos({ requisitos }: { requisitos: readonly RequisitoTexto[] }) {
  if (requisitos.length === 0) return <p className="text-slate-600">Sin requisitos registrados.</p>;
  return (
    <div className="flex flex-col gap-4">
      {agruparRequisitos(requisitos).map((grupo) => (
        <div key={grupo.nivel}>
          <p className="text-sm font-semibold tracking-wide text-slate-600 uppercase">{grupo.nivel}</p>
          <ul className="mt-2 flex flex-col gap-2 text-slate-800">
            {grupo.textos.map((texto, i) => (
              <li key={i} className="flex items-start gap-2.5">
                <span className="mt-0.5 grid size-5 shrink-0 place-items-center rounded-full bg-emerald-100 text-emerald-800">
                  <Icono nombre="check" className="size-3.5" />
                </span>
                <span className="whitespace-pre-line">{texto}</span>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
