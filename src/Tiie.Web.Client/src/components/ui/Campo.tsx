import { useId, type ComponentProps, type ReactNode } from 'react';

export interface CampoProps extends Omit<ComponentProps<'input'>, 'id'> {
  etiqueta: string;
  /** Mensaje de error del campo; se asocia con aria-describedby. */
  error?: string | undefined;
  /** Texto de ayuda bajo la etiqueta. */
  ayuda?: ReactNode;
  id?: string;
}

/** Campo de texto con etiqueta visible y error asociado (WCAG 2.1 AA). */
export function Campo({ etiqueta, error, ayuda, id, className = '', ...props }: CampoProps) {
  const generado = useId();
  const idCampo = id ?? generado;
  const idAyuda = `${idCampo}-ayuda`;
  const idError = `${idCampo}-error`;
  const describe = [ayuda ? idAyuda : null, error ? idError : null].filter(Boolean).join(' ') || undefined;

  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <label htmlFor={idCampo} className="font-medium text-slate-800">
        {etiqueta}
      </label>
      {ayuda && (
        <p id={idAyuda} className="text-sm text-slate-600">
          {ayuda}
        </p>
      )}
      <input
        id={idCampo}
        aria-invalid={error ? true : undefined}
        aria-describedby={describe}
        className={`min-h-11 rounded-md border bg-white px-3 py-2 text-base text-slate-900 ${
          error ? 'border-red-700' : 'border-slate-400'
        }`}
        {...props}
      />
      {error && (
        <p id={idError} className="text-sm font-medium text-red-800">
          {error}
        </p>
      )}
    </div>
  );
}

export interface CasillaProps extends Omit<ComponentProps<'input'>, 'id' | 'type'> {
  etiqueta: string;
  id?: string;
}

/** Casilla de verificación con etiqueta clicable de 44 px de alto. */
export function Casilla({ etiqueta, id, ...props }: CasillaProps) {
  const generado = useId();
  const idCampo = id ?? generado;
  return (
    <div className="flex min-h-11 items-center gap-3">
      <input id={idCampo} type="checkbox" className="size-5 accent-guinda-700" {...props} />
      <label htmlFor={idCampo} className="text-slate-800">
        {etiqueta}
      </label>
    </div>
  );
}
