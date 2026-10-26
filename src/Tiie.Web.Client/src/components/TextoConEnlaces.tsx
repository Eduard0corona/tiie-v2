import { Link } from 'react-router';
import { tramosDeTexto } from '@/lib/enlaces';

const estiloEnlace = 'font-semibold text-guinda-700 underline underline-offset-4 hover:text-guinda-900';

/**
 * Texto plano que escribe el administrador, con enlaces `[texto](destino)`. Nunca interpreta HTML. Los enlaces
 * externos abren en otra pestaña y lo dicen a los lectores de pantalla.
 */
export function TextoConEnlaces({
  texto,
  claseEnlace = estiloEnlace,
}: {
  texto: string;
  claseEnlace?: string;
}) {
  return tramosDeTexto(texto).map((tramo, i) => {
    if (tramo.tipo === 'texto') return tramo.texto;
    if (tramo.interno)
      return (
        <Link key={i} to={tramo.destino} className={claseEnlace}>
          {tramo.texto}
        </Link>
      );
    const externo = tramo.destino.toLowerCase().startsWith('https://');
    return (
      <a
        key={i}
        href={tramo.destino}
        className={claseEnlace}
        {...(externo ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
      >
        {tramo.texto}
        {externo && (
          <>
            {' '}
            <span className="sr-only">(abre en una pestaña nueva)</span>
          </>
        )}
      </a>
    );
  });
}
