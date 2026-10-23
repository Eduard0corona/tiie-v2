import { Link } from 'react-router';

/** Logotipo del sitio: la tarjeta en guinda y el nombre corto. En el panel lleva a la agenda. */
export function Marca({
  a = '/',
  subtitulo = 'Sinaloa',
  etiqueta = 'Citas TIIE Sinaloa, inicio',
}: {
  a?: string;
  subtitulo?: string;
  etiqueta?: string;
}) {
  return (
    <Link to={a} className="flex min-h-11 items-center gap-2.5 rounded-lg" aria-label={etiqueta}>
      <svg viewBox="0 0 32 32" className="size-9 shrink-0" aria-hidden="true" focusable="false">
        <rect width="32" height="32" rx="8" fill="#7b1e3a" />
        <rect x="6" y="9" width="20" height="14" rx="2.5" fill="none" stroke="#fff" strokeWidth="2" />
        <rect x="9" y="13" width="5" height="4" rx="1" fill="#d5b685" />
        <path d="M17 14h6M17 18h4" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
      </svg>
      <span className="flex flex-col leading-none">
        <span className="text-lg font-extrabold tracking-tight text-guinda-800">Citas TIIE</span>
        <span className="mt-0.5 text-xs font-medium text-slate-600">{subtitulo}</span>
      </span>
    </Link>
  );
}
