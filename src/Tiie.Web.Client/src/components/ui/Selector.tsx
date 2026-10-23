import { forwardRef, useId, type ComponentProps, type ReactNode } from 'react';

export interface SelectorProps extends Omit<ComponentProps<'select'>, 'id'> {
  etiqueta: string;
  error?: string | undefined;
  ayuda?: ReactNode;
  id?: string;
}

/** Lista desplegable con etiqueta visible y error asociado. */
export const Selector = forwardRef<HTMLSelectElement, SelectorProps>(function Selector(
  { etiqueta, error, ayuda, id, className = '', children, ...props },
  ref,
) {
  const generado = useId();
  const idCampo = id ?? generado;
  const describe =
    [ayuda ? `${idCampo}-ayuda` : null, error ? `${idCampo}-error` : null].filter(Boolean).join(' ') ||
    undefined;
  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <label htmlFor={idCampo} className="font-medium text-slate-800">
        {etiqueta}
      </label>
      {ayuda && (
        <p id={`${idCampo}-ayuda`} className="text-sm text-slate-600">
          {ayuda}
        </p>
      )}
      <select
        ref={ref}
        id={idCampo}
        aria-invalid={error ? true : undefined}
        aria-describedby={describe}
        className={`min-h-11 rounded-md border bg-white px-3 py-2 text-base text-slate-900 ${
          error ? 'border-red-700' : 'border-slate-400'
        }`}
        {...props}
      >
        {children}
      </select>
      {error && (
        <p id={`${idCampo}-error`} className="text-sm font-medium text-red-800">
          {error}
        </p>
      )}
    </div>
  );
});

export interface AreaTextoProps extends Omit<ComponentProps<'textarea'>, 'id'> {
  etiqueta: string;
  error?: string | undefined;
  ayuda?: ReactNode;
  id?: string;
}

/** Texto de varias líneas (requisitos, notas, motivos) con etiqueta y error asociado. */
export const AreaTexto = forwardRef<HTMLTextAreaElement, AreaTextoProps>(function AreaTexto(
  { etiqueta, error, ayuda, id, className = '', rows = 3, ...props },
  ref,
) {
  const generado = useId();
  const idCampo = id ?? generado;
  const describe =
    [ayuda ? `${idCampo}-ayuda` : null, error ? `${idCampo}-error` : null].filter(Boolean).join(' ') ||
    undefined;
  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <label htmlFor={idCampo} className="font-medium text-slate-800">
        {etiqueta}
      </label>
      {ayuda && (
        <p id={`${idCampo}-ayuda`} className="text-sm text-slate-600">
          {ayuda}
        </p>
      )}
      <textarea
        ref={ref}
        id={idCampo}
        rows={rows}
        aria-invalid={error ? true : undefined}
        aria-describedby={describe}
        className={`rounded-md border bg-white px-3 py-2 text-base text-slate-900 ${
          error ? 'border-red-700' : 'border-slate-400'
        }`}
        {...props}
      />
      {error && (
        <p id={`${idCampo}-error`} className="text-sm font-medium text-red-800">
          {error}
        </p>
      )}
    </div>
  );
});
