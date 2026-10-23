import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';
import { PASOS, useEstadoAsistente, type Paso } from './estadoAsistente';
import { PasoDatos } from './PasoDatos';
import { PasoFecha } from './PasoFecha';
import { PasoHora } from './PasoHora';
import { PasoSede } from './PasoSede';
import { ResumenSeleccion } from './ResumenSeleccion';

type Asistente = ReturnType<typeof useEstadoAsistente>;

/**
 * Asistente de reserva en cuatro pasos: sede y trámite, fecha, hora y datos (RF-03). La selección vive en la
 * URL; los datos personales, solo en el formulario. En escritorio, el resumen acompaña a la derecha.
 */
export function PaginaAgendar() {
  const asistente = useEstadoAsistente();
  return (
    <Contenedor ancho="amplio" className="py-6 sm:py-10">
      <Progreso paso={asistente.paso} ir={asistente.ir} />
      <div className="mt-8 grid gap-10 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
        <div className="min-w-0">
          {asistente.paso === 'sede' && <PasoSede {...asistente} />}
          {asistente.paso === 'fecha' && <PasoFecha {...asistente} />}
          {asistente.paso === 'hora' && <PasoHora {...asistente} />}
          {asistente.paso === 'datos' && <PasoDatos {...asistente} />}
        </div>
        <aside className="hidden lg:sticky lg:top-24 lg:block">
          <ResumenSeleccion seleccion={asistente.seleccion} lateral />
        </aside>
      </div>
    </Contenedor>
  );
}

/** Pasos con su estado. Los ya hechos son botones para volver; el actual lleva aria-current. */
function Progreso({ paso, ir }: { paso: Paso; ir: Asistente['ir'] }) {
  const actual = PASOS.findIndex((p) => p.paso === paso);
  return (
    <nav aria-label="Pasos para agendar" className="mx-auto max-w-2xl lg:mx-0">
      <p className="text-sm font-semibold text-guinda-700">
        Paso {actual + 1} de {PASOS.length}
      </p>
      <ol className="mt-3 flex items-start">
        {PASOS.map((p, i) => {
          const hecho = i < actual;
          const esActual = i === actual;
          const circulo = (
            <span
              className={`grid size-9 place-items-center rounded-full text-sm font-bold ring-4 ring-slate-50 transition-colors ${
                hecho
                  ? 'bg-guinda-700 text-white group-hover:bg-guinda-800'
                  : esActual
                    ? 'border-2 border-guinda-700 bg-white text-guinda-700'
                    : 'border-2 border-slate-300 bg-white text-slate-500'
              }`}
            >
              {hecho ? <Icono nombre="check" className="size-4.5" /> : i + 1}
            </span>
          );
          const etiqueta = (
            <span
              className={`text-xs leading-tight sm:text-sm ${
                esActual
                  ? 'font-bold text-slate-950'
                  : hecho
                    ? 'font-medium text-slate-700'
                    : 'text-slate-500'
              }`}
            >
              {p.titulo}
              <span className="sr-only">{hecho ? ', completado' : esActual ? '' : ', pendiente'}</span>
            </span>
          );
          return (
            <li
              key={p.paso}
              aria-current={esActual ? 'step' : undefined}
              className="relative flex flex-1 flex-col items-center text-center"
            >
              {i < PASOS.length - 1 && (
                <span
                  aria-hidden="true"
                  className={`absolute top-[1.125rem] left-1/2 h-0.5 w-full ${i < actual ? 'bg-guinda-700' : 'bg-slate-200'}`}
                />
              )}
              {hecho ? (
                <button
                  type="button"
                  onClick={() => ir({ paso: p.paso })}
                  className="group relative flex flex-col items-center gap-2 rounded-lg px-1"
                >
                  {circulo}
                  {etiqueta}
                </button>
              ) : (
                <span className="relative flex flex-col items-center gap-2 px-1">
                  {circulo}
                  {etiqueta}
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
