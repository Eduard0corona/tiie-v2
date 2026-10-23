import { Link, isRouteErrorResponse, useRouteError } from 'react-router';

/** Error inesperado al cargar o dibujar una pantalla. */
export function PaginaError() {
  const error = useRouteError();
  const noEncontrada = isRouteErrorResponse(error) && error.status === 404;
  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <h1 className="text-2xl font-bold">{noEncontrada ? 'Página no encontrada' : 'Algo salió mal'}</h1>
      <p className="mt-2 text-slate-700">
        {noEncontrada
          ? 'La dirección que abriste no existe.'
          : 'No pudimos mostrar esta página. Recárgala o vuelve al inicio.'}
      </p>
      <Link
        to="/"
        className="mt-6 inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
      >
        Ir al inicio
      </Link>
    </main>
  );
}
