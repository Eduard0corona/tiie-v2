import type { ReactNode } from 'react';

/**
 * Íconos de trazo propios (24 × 24, `currentColor`), sin librería: el sitio público no carga un kit completo
 * por una docena de figuras. Son decorativos: el texto de al lado siempre dice lo mismo.
 */
const trazos = {
  flecha: <path d="M5 12h14M13 6l6 6-6 6" />,
  flechaIzquierda: <path d="M19 12H5M11 6l-6 6 6 6" />,
  check: <path d="M5 12.5l4.5 4.5L19 7.5" />,
  calendario: (
    <>
      <rect x="3" y="5" width="18" height="16" rx="2.5" />
      <path d="M3 10h18M8 3v4M16 3v4" />
    </>
  ),
  reloj: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3.5 2" />
    </>
  ),
  ubicacion: (
    <>
      <path d="M12 21s-7-6.1-7-11.5a7 7 0 0 1 14 0C19 14.9 12 21 12 21z" />
      <circle cx="12" cy="9.5" r="2.5" />
    </>
  ),
  correo: (
    <>
      <rect x="3" y="5" width="18" height="14" rx="2.5" />
      <path d="M3.5 7l8.5 6.5L20.5 7" />
    </>
  ),
  descargar: <path d="M12 4v11M7 10l5 5 5-5M5 20h14" />,
  copiar: (
    <>
      <rect x="9" y="9" width="11" height="11" rx="2" />
      <path d="M15 9V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h3" />
    </>
  ),
  externo: <path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5" />,
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8h.01" />
    </>
  ),
  alerta: <path d="M12 3.5l9 16H3l9-16zM12 10v4M12 17h.01" />,
  error: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M9 9l6 6M15 9l-6 6" />
    </>
  ),
  exito: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M8 12.5l2.7 2.7L16 9.8" />
    </>
  ),
  usuario: (
    <>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21a8 8 0 0 1 16 0" />
    </>
  ),
  escudo: (
    <>
      <path d="M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6l8-3z" />
      <path d="M9 12l2 2 4-4" />
    </>
  ),
  tarjeta: (
    <>
      <rect x="2.5" y="5" width="19" height="14" rx="2.5" />
      <circle cx="8.5" cy="11" r="2" />
      <path d="M5.5 16c.7-1.3 1.8-2 3-2s2.3.7 3 2M14.5 10h4M14.5 14h4" />
    </>
  ),
  documento: (
    <>
      <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5z" />
      <path d="M14 3v5h5M9 13h6M9 17h4" />
    </>
  ),
  sede: <path d="M4 21V9l8-5.5L20 9v12M9 21v-6h6v6M3 21h18" />,
  celular: (
    <>
      <rect x="6.5" y="2.5" width="11" height="19" rx="2.5" />
      <path d="M11 18h2" />
    </>
  ),
  manana: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4" />
    </>
  ),
  tarde: (
    <path d="M3 18h18M6.5 18a5.5 5.5 0 0 1 11 0M12 6.5v2.5M5.4 10.9l1.6 1.4M18.6 10.9L17 12.3M3 21h18" />
  ),
  cerrar: <path d="M6 6l12 12M18 6L6 18" />,
  cambiar: <path d="M4 12a8 8 0 0 1 14-5.3M18.5 3v4h-4M20 12a8 8 0 0 1-14 5.3M5.5 21v-4h4" />,
  salir: <path d="M15 4h3.5a1.5 1.5 0 0 1 1.5 1.5v13a1.5 1.5 0 0 1-1.5 1.5H15M10 17l5-5-5-5M15 12H3.5" />,
  chevronAbajo: <path d="M6 9l6 6 6-6" />,
  rayo: <path d="M13 2.5L4.5 13.5H12l-1 8 8.5-11H12l1-8z" />,
} satisfies Record<string, ReactNode>;

export type NombreIcono = keyof typeof trazos;

/** `className` agrega color o margen; el tamaño es `size-5` salvo que traiga otro `size-*`. */
export function Icono({ nombre, className = '' }: { nombre: NombreIcono; className?: string }) {
  const clases = /(^|\s)size-/.test(className) ? className : `size-5 ${className}`;
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      className={`shrink-0 ${clases}`}
    >
      {trazos[nombre]}
    </svg>
  );
}
