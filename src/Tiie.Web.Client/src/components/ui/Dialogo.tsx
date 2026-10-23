import { useEffect, useId, useRef, type ReactNode } from 'react';

export interface DialogoProps {
  titulo: string;
  alCerrar: () => void;
  children: ReactNode;
}

/**
 * Diálogo modal accesible: el foco entra al abrir, Escape cierra y el foco vuelve al control que lo abrió.
 * Se dibuja solo mientras está abierto.
 */
export function Dialogo({ titulo, alCerrar, children }: DialogoProps) {
  const idTitulo = useId();
  const panel = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const anterior = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const primero = panel.current?.querySelector<HTMLElement>('input, textarea, button, [href]');
    (primero ?? panel.current)?.focus();
    return () => anterior?.focus();
  }, []);

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => {
      if (e.key === 'Escape') alCerrar();
    };
    document.addEventListener('keydown', alTeclear);
    return () => document.removeEventListener('keydown', alTeclear);
  }, [alCerrar]);

  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center bg-slate-950/50 p-4 backdrop-blur-sm sm:items-center">
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={idTitulo}
        tabIndex={-1}
        className="flex max-h-[90dvh] w-full max-w-md flex-col gap-4 overflow-y-auto rounded-2xl bg-white p-6 shadow-xl"
      >
        <h2 id={idTitulo} className="text-xl font-bold">
          {titulo}
        </h2>
        {children}
      </div>
    </div>
  );
}
