import { Link } from 'react-router';

/** Inicio provisional: trámites, requisitos y sedes llegan en la Fase 2 (RF-01). */
export function PaginaInicio() {
  return (
    <section aria-labelledby="titulo-inicio" className="flex flex-col gap-4">
      <h1 id="titulo-inicio" className="text-2xl font-bold sm:text-3xl">
        Agenda tu cita para la Tarjeta Inteligente de Identidad Estudiantil
      </h1>
      <p className="text-lg text-slate-700">La agenda en línea estará disponible pronto.</p>
      <p className="text-slate-700">
        Aquí podrás consultar los trámites, sus requisitos y las sedes de atención en Sinaloa, y reservar tu
        cita desde el celular.
      </p>
      <p>
        <Link
          to="/aviso-de-privacidad"
          className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
        >
          Consulta el aviso de privacidad
        </Link>
      </p>
    </section>
  );
}
