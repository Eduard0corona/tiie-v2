import type { components } from '@/api/esquema';
import { agruparRequisitos } from '@/features/publico/comun/formatos';

type RequisitoTexto = components['schemas']['RequisitoTexto'];

/** Requisitos agrupados por nivel escolar. Son texto plano: los saltos de línea se respetan. */
export function Requisitos({ requisitos }: { requisitos: readonly RequisitoTexto[] }) {
  if (requisitos.length === 0) return <p className="text-slate-600">Sin requisitos registrados.</p>;
  return (
    <div className="flex flex-col gap-3">
      {agruparRequisitos(requisitos).map((grupo) => (
        <div key={grupo.nivel}>
          <p className="font-medium text-slate-800">{grupo.nivel}</p>
          <ul className="mt-1 list-disc pl-5 text-slate-700">
            {grupo.textos.map((texto, i) => (
              <li key={i} className="whitespace-pre-line">
                {texto}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
