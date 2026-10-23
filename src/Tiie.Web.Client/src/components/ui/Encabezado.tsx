import { useEffect, useRef, type ReactNode } from 'react';

/**
 * Título de una pantalla o de un paso. Recibe el foco al aparecer, para que el lector de pantalla
 * anuncie el cambio de paso (docs/08#accesibilidad).
 */
export function EncabezadoPaso({ children, className = '' }: { children: ReactNode; className?: string }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return (
    <h1
      ref={ref}
      tabIndex={-1}
      className={`text-2xl font-bold text-slate-900 focus:outline-none ${className}`}
    >
      {children}
    </h1>
  );
}

export function Cargando({ texto = 'Cargando…' }: { texto?: string }) {
  return (
    <p role="status" className="flex items-center gap-3 p-6 text-slate-600">
      <span
        aria-hidden="true"
        className="size-5 shrink-0 animate-spin rounded-full border-2 border-guinda-200 border-t-guinda-700"
      />
      {texto}
    </p>
  );
}
