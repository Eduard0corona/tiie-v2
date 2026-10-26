import { Link } from 'react-router';
import type { components } from '@/api/esquema';
import { mensajeDeError } from '@/api/errores';
import { TextoConEnlaces } from '@/components/TextoConEnlaces';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';
import { EncabezadoSeccion } from './EncabezadoSeccion';

const enlace = 'font-semibold text-guinda-700 underline underline-offset-4 hover:text-guinda-900';

type Pregunta = components['schemas']['PreguntaPortada'];

/**
 * Las dudas que hoy se resuelven por teléfono o en el módulo. Las escribe el administrador; los plazos y límites
 * concretos los dice cada cita, porque son configurables (docs/03). Sin preguntas, la sección no se muestra.
 */
export function PreguntasFrecuentes({
  preguntas,
  error,
}: {
  preguntas?: readonly Pregunta[];
  error?: unknown;
}) {
  if (preguntas?.length === 0) return null;
  return (
    <section id="preguntas" aria-labelledby="titulo-preguntas" className="scroll-mt-20 py-16 sm:py-20">
      <Contenedor ancho="amplio" className="grid gap-10 lg:grid-cols-[0.8fr_1.2fr]">
        <div className="flex flex-col gap-6">
          <EncabezadoSeccion
            id="titulo-preguntas"
            antetitulo="Resolvemos tus dudas"
            titulo="Preguntas frecuentes"
          />
          <p className="text-slate-600">
            ¿Ya tienes cita y necesitas verla?{' '}
            <Link to="/mi-cita" className={enlace}>
              Consulta tu cita
            </Link>
            .
          </p>
        </div>
        <div className="divide-y divide-slate-200 rounded-2xl border border-slate-200 bg-white px-5 shadow-tarjeta sm:px-6">
          {!preguntas && !error && <p className="py-5 text-slate-600">Cargando preguntas…</p>}
          {error !== undefined && error !== null && (
            <p className="py-5 text-slate-700">{mensajeDeError(error)}</p>
          )}
          {preguntas?.map(({ pregunta, respuesta }, i) => (
            <details key={`${i}-${pregunta}`} className="group">
              <summary className="flex min-h-14 items-center justify-between gap-4 py-4 font-semibold text-slate-900">
                {pregunta}
                <span className="grid size-8 shrink-0 place-items-center rounded-full bg-slate-100 text-slate-700 transition-transform group-open:rotate-180">
                  <Icono nombre="chevronAbajo" className="size-4" />
                </span>
              </summary>
              <p className="pb-5 whitespace-pre-line text-slate-700">
                <TextoConEnlaces texto={respuesta} claseEnlace={enlace} />
              </p>
            </details>
          ))}
        </div>
      </Contenedor>
    </section>
  );
}
