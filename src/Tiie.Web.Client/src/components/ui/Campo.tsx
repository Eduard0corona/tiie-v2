import { useId, type ComponentProps, type ReactNode } from 'react';
import { Icono } from './Icono';

export interface CampoProps extends Omit<ComponentProps<'input'>, 'id'> {
  etiqueta: string;
  /** Mensaje de error del campo; se asocia con aria-describedby. */
  error?: string | undefined;
  /** Texto de ayuda bajo la etiqueta. */
  ayuda?: ReactNode;
  /** Retroalimentación positiva bajo el campo (por ejemplo, «CURP válida»); no se muestra si hay error. */
  confirmacion?: ReactNode;
  id?: string;
}

export const estiloEntrada = (conError: boolean) =>
  `min-h-12 w-full rounded-lg border bg-white px-3.5 py-2.5 text-base text-slate-900 shadow-sm transition-colors placeholder:text-slate-400 hover:border-slate-500 ${
    conError ? 'border-red-700' : 'border-slate-400'
  }`;

/** Campo de texto con etiqueta visible y error asociado (WCAG 2.1 AA). */
export function Campo({ etiqueta, error, ayuda, confirmacion, id, className = '', ...props }: CampoProps) {
  const generado = useId();
  const idCampo = id ?? generado;
  const idAyuda = `${idCampo}-ayuda`;
  const idError = `${idCampo}-error`;
  const idConfirmacion = `${idCampo}-confirmacion`;
  const conConfirmacion = !error && confirmacion;
  const describe =
    [ayuda ? idAyuda : null, error ? idError : null, conConfirmacion ? idConfirmacion : null]
      .filter(Boolean)
      .join(' ') || undefined;

  return (
    <div className={`flex flex-col gap-1.5 ${className}`}>
      <label htmlFor={idCampo} className="font-semibold text-slate-800">
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
        className={estiloEntrada(Boolean(error))}
        {...props}
      />
      {error && (
        <p id={idError} className="flex items-start gap-1.5 text-sm font-medium text-red-800">
          <Icono nombre="error" className="mt-px size-4" />
          {error}
        </p>
      )}
      {conConfirmacion && (
        <p id={idConfirmacion} className="flex items-start gap-1.5 text-sm font-medium text-emerald-800">
          <Icono nombre="exito" className="mt-px size-4" />
          <span>{confirmacion}</span>
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
      <input id={idCampo} type="checkbox" className="size-5 shrink-0 accent-guinda-700" {...props} />
      <label htmlFor={idCampo} className="font-medium text-slate-800">
        {etiqueta}
      </label>
    </div>
  );
}
