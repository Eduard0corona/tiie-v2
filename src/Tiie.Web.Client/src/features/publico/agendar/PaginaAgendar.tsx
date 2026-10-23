import { PASOS, useEstadoAsistente, type Paso } from './estadoAsistente';
import { PasoDatos } from './PasoDatos';
import { PasoFecha } from './PasoFecha';
import { PasoHora } from './PasoHora';
import { PasoSede } from './PasoSede';

/**
 * Asistente de reserva en cuatro pasos: sede y trámite, fecha, hora y datos (RF-03). La selección vive en la
 * URL; los datos personales, solo en el formulario.
 */
export function PaginaAgendar() {
  const asistente = useEstadoAsistente();
  return (
    <div className="flex flex-col gap-6">
      <Progreso paso={asistente.paso} />
      {asistente.paso === 'sede' && <PasoSede {...asistente} />}
      {asistente.paso === 'fecha' && <PasoFecha {...asistente} />}
      {asistente.paso === 'hora' && <PasoHora {...asistente} />}
      {asistente.paso === 'datos' && <PasoDatos {...asistente} />}
    </div>
  );
}

function Progreso({ paso }: { paso: Paso }) {
  const actual = PASOS.findIndex((p) => p.paso === paso);
  return (
    <nav aria-label="Pasos para agendar">
      <p className="text-sm text-slate-600">
        Paso {actual + 1} de {PASOS.length}
      </p>
      <ol className="mt-2 grid grid-cols-4 gap-1">
        {PASOS.map((p, i) => (
          <li
            key={p.paso}
            aria-current={i === actual ? 'step' : undefined}
            className={`border-t-4 pt-1 text-xs sm:text-sm ${
              i <= actual ? 'border-guinda-700 text-slate-900' : 'border-slate-300 text-slate-500'
            } ${i === actual ? 'font-semibold' : ''}`}
          >
            {p.titulo}
          </li>
        ))}
      </ol>
    </nav>
  );
}
