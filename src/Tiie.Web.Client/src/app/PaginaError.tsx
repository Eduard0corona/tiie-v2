import { Link, isRouteErrorResponse, useRouteError } from 'react-router';
import { estiloBoton } from '@/components/ui/Boton';

/** Error inesperado al cargar o dibujar una pantalla. */
export function PaginaError() {
  const error = useRouteError();
  const noEncontrada = isRouteErrorResponse(error) && error.status === 404;
  return (
    <main className="mx-auto flex max-w-xl flex-col items-start px-4 py-16">
      <h1 className="text-3xl font-bold tracking-tight">
        {noEncontrada ? 'Página no encontrada' : 'Algo salió mal'}
      </h1>
      <p className="mt-2 text-slate-700">
        {noEncontrada
          ? 'La dirección que abriste no existe.'
          : 'No pudimos mostrar esta página. Recárgala o vuelve al inicio.'}
      </p>
      <Link to="/" className={`${estiloBoton()} mt-6`}>
        Ir al inicio
      </Link>
    </main>
  );
}
