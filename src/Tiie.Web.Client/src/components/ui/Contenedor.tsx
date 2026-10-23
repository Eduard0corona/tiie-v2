import type { ReactNode } from 'react';

const anchos = {
  angosto: 'max-w-2xl',
  medio: 'max-w-3xl',
  amplio: 'max-w-6xl',
} as const;

/** Ancho de lectura de cada pantalla pública, con el mismo margen lateral en todas. */
export function Contenedor({
  ancho = 'medio',
  className = '',
  children,
}: {
  ancho?: keyof typeof anchos;
  className?: string;
  children: ReactNode;
}) {
  return <div className={`mx-auto w-full px-4 sm:px-6 ${anchos[ancho]} ${className}`}>{children}</div>;
}
