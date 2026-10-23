import type { ReactNode } from 'react';

type Tono = 'error' | 'exito' | 'info';

const estilos: Record<Tono, string> = {
  error: 'border-red-700 bg-red-50 text-red-900',
  exito: 'border-green-700 bg-green-50 text-green-900',
  info: 'border-slate-400 bg-white text-slate-900',
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
  return (
    <div className={`rounded-md border-l-4 p-4 ${estilos[tono]}`}>
      {titulo && <p className="font-semibold">{titulo}</p>}
      {children && <div className={titulo ? 'mt-1' : undefined}>{children}</div>}
      {traza && <p className="mt-2 text-xs opacity-75">Referencia: {traza}</p>}
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
