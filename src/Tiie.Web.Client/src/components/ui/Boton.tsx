import type { ComponentProps } from 'react';

type Variante = 'primario' | 'secundario' | 'enlace';

const estilos: Record<Variante, string> = {
  primario:
    'bg-guinda-700 text-white hover:bg-guinda-800 active:bg-guinda-900 disabled:bg-slate-400 disabled:text-white',
  secundario:
    'border border-slate-300 bg-white text-slate-900 hover:bg-slate-100 disabled:text-slate-400 disabled:hover:bg-white',
  enlace: 'text-guinda-700 underline underline-offset-4 hover:text-guinda-900 disabled:text-slate-400',
};

export interface BotonProps extends ComponentProps<'button'> {
  variante?: Variante;
  /** Muestra el texto de espera y deshabilita el botón. */
  ocupado?: boolean;
  textoOcupado?: string;
}

/** Botón con objetivo táctil de 44 px como mínimo. */
export function Boton({
  variante = 'primario',
  ocupado = false,
  textoOcupado = 'Espera…',
  className = '',
  disabled,
  children,
  type = 'button',
  ...props
}: BotonProps) {
  return (
    <button
      type={type}
      disabled={disabled || ocupado}
      aria-busy={ocupado || undefined}
      className={`inline-flex min-h-11 items-center justify-center rounded-md px-4 py-2 font-medium transition-colors disabled:cursor-not-allowed ${estilos[variante]} ${className}`}
      {...props}
    >
      {ocupado ? textoOcupado : children}
    </button>
  );
}
