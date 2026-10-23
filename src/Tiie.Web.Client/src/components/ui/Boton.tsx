import type { ComponentProps } from 'react';

export type Variante = 'primario' | 'secundario' | 'enlace' | 'peligro' | 'destructivo' | 'claro';
export type Tamano = 'normal' | 'grande';

const estilos: Record<Variante, string> = {
  primario:
    'bg-guinda-700 text-white shadow-sm hover:bg-guinda-800 active:bg-guinda-900 disabled:bg-slate-300 disabled:text-slate-600 disabled:shadow-none',
  secundario:
    'border border-slate-300 bg-white text-slate-900 shadow-sm hover:border-slate-400 hover:bg-slate-50 disabled:text-slate-400 disabled:hover:bg-white',
  enlace: 'text-guinda-700 underline underline-offset-4 hover:text-guinda-900 disabled:text-slate-400',
  peligro:
    'border border-red-200 bg-white text-red-800 hover:border-red-300 hover:bg-red-50 disabled:text-slate-400',
  destructivo:
    'bg-red-700 text-white shadow-sm hover:bg-red-800 disabled:bg-slate-300 disabled:text-slate-600',
  claro: 'bg-white text-guinda-800 shadow-sm hover:bg-guinda-50',
};

const tamanos: Record<Tamano, string> = {
  normal: 'min-h-11 rounded-lg px-4 py-2',
  grande: 'min-h-13 rounded-xl px-6 py-3 text-base sm:text-lg',
};

export interface EstiloBoton {
  variante?: Variante;
  tamano?: Tamano;
  /** Ocupa todo el ancho; en pantallas medianas vuelve a su ancho natural. */
  bloque?: boolean;
}

/** Clases de un botón, también para enlaces que se ven como botón (`<Link className={estiloBoton()}>`). */
export function estiloBoton({ variante = 'primario', tamano = 'normal', bloque = false }: EstiloBoton = {}) {
  const forma = variante === 'enlace' ? 'min-h-11 py-2' : tamanos[tamano];
  return `inline-flex items-center justify-center gap-2 font-semibold transition-colors disabled:cursor-not-allowed ${forma} ${estilos[variante]} ${bloque ? 'w-full sm:w-auto' : ''}`;
}

export interface BotonProps extends ComponentProps<'button'>, EstiloBoton {
  /** Muestra el texto de espera y deshabilita el botón. */
  ocupado?: boolean;
  textoOcupado?: string;
}

/** Botón con objetivo táctil de 44 px como mínimo. */
export function Boton({
  variante = 'primario',
  tamano = 'normal',
  bloque = false,
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
      className={`${estiloBoton({ variante, tamano, bloque })} ${className}`}
      {...props}
    >
      {ocupado ? (
        <>
          <span
            aria-hidden="true"
            className="size-4 animate-spin rounded-full border-2 border-current border-r-transparent"
          />
          {textoOcupado}
        </>
      ) : (
        children
      )}
    </button>
  );
}
