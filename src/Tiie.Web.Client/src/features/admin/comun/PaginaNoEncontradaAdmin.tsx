import { Link } from 'react-router';

/** Una dirección de /admin que no existe: se queda en el panel en lugar de mostrar el 404 público. */
export function PaginaNoEncontradaAdmin() {
  return (
    <section aria-labelledby="titulo-no-encontrada-admin" className="flex flex-col gap-4">
      <h1 id="titulo-no-encontrada-admin" className="text-2xl font-bold">
        Página no encontrada
      </h1>
      <p className="text-slate-700">Esa sección del panel no existe o cambió de dirección.</p>
      <p>
        <Link
          to="/admin/agenda"
          className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
        >
          Ir a la agenda
        </Link>
      </p>
    </section>
  );
}
