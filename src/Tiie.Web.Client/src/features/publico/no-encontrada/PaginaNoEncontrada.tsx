import { Link } from 'react-router';

export function PaginaNoEncontrada() {
  return (
    <section aria-labelledby="titulo-no-encontrada" className="flex flex-col gap-4">
      <h1 id="titulo-no-encontrada" className="text-2xl font-bold">
        Página no encontrada
      </h1>
      <p className="text-slate-700">La dirección que abriste no existe o cambió.</p>
      <p>
        <Link
          to="/"
          className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
        >
          Ir al inicio
        </Link>
      </p>
    </section>
  );
}
