import type { ReactNode } from 'react';
import { Icono, type NombreIcono } from './Icono';

type Tono = 'error' | 'exito' | 'info' | 'aviso';

const estilos: Record<Tono, { caja: string; icono: string; nombre: NombreIcono }> = {
  error: { caja: 'border-red-200 bg-red-50 text-red-950', icono: 'text-red-700', nombre: 'error' },
  exito: {
    caja: 'border-emerald-200 bg-emerald-50 text-emerald-950',
    icono: 'text-emerald-700',
    nombre: 'exito',
  },
  info: { caja: 'border-slate-200 bg-white text-slate-900', icono: 'text-slate-500', nombre: 'info' },
  aviso: { caja: 'border-amber-200 bg-amber-50 text-amber-950', icono: 'text-amber-700', nombre: 'alerta' },
};

export interface AlertaProps {
  tono?: Tono;
  titulo?: string;
  children?: ReactNode;
  /** traceId de la API, en letra pequeña para soporte. */
  traza?: string | null;
}

/** Mensaje destacado. Va dentro de una región aria-live para que se anuncie. */
export function Alerta({ tono = 'info', titulo, children, traza }: AlertaProps) {
  const estilo = estilos[tono];
  return (
    <div className={`flex gap-3 rounded-xl border p-4 ${estilo.caja}`}>
      <Icono nombre={estilo.nombre} className={`mt-0.5 size-5 ${estilo.icono}`} />
      <div className="min-w-0 flex-1">
        {titulo && <p className="font-semibold">{titulo}</p>}
        {children && <div className={titulo ? 'mt-1' : undefined}>{children}</div>}
        {traza && <p className="mt-2 text-xs opacity-75">Referencia: {traza}</p>}
      </div>
    </div>
  );
}

/** Región que anuncia sus cambios a los lectores de pantalla sin interrumpir (docs/08#accesibilidad). */
export function RegionAnuncios({ children }: { children?: ReactNode }) {
  return (
    <div aria-live="polite" aria-atomic="true">
      {children}
    </div>
  );
}
