import type { ReactNode } from 'react';
import { Icono, type NombreIcono } from './Icono';

export type TonoInsignia = 'neutro' | 'exito' | 'aviso' | 'error' | 'marca';

const tonos: Record<TonoInsignia, string> = {
  neutro: 'bg-slate-100 text-slate-800 ring-slate-200',
  exito: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  aviso: 'bg-amber-50 text-amber-900 ring-amber-200',
  error: 'bg-red-50 text-red-800 ring-red-200',
  marca: 'bg-guinda-50 text-guinda-800 ring-guinda-200',
};

/** Etiqueta corta (costo, estado). El texto siempre dice el estado; el color solo acompaña. */
export function Insignia({
  tono = 'neutro',
  icono,
  children,
}: {
  tono?: TonoInsignia;
  icono?: NombreIcono;
  children: ReactNode;
}) {
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1 rounded-full px-2.5 py-0.5 text-sm font-semibold whitespace-nowrap ring-1 ring-inset ${tonos[tono]}`}
    >
      {icono && <Icono nombre={icono} className="size-4" />}
      {children}
    </span>
  );
}
