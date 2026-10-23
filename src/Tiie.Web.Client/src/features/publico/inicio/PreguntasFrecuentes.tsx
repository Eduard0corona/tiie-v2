import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';
import { EncabezadoSeccion } from './EncabezadoSeccion';

const enlace = 'font-semibold text-guinda-700 underline underline-offset-4 hover:text-guinda-900';

/**
 * Las dudas que hoy se resuelven por teléfono o en el módulo. Las respuestas describen cómo funciona el sitio;
 * los plazos y límites concretos los dice cada cita, porque son configurables (docs/03).
 */
const preguntas: readonly { pregunta: string; respuesta: ReactNode }[] = [
  {
    pregunta: '¿Necesito crear una cuenta?',
    respuesta:
      'No. Solo necesitas la CURP de la persona que hará el trámite y un correo electrónico. Ahí te enviamos el comprobante y un enlace privado para consultar tu cita.',
  },
  {
    pregunta: '¿Puedo agendar la cita de mi hija o hijo?',
    respuesta:
      'Sí. Escribe los datos de la persona estudiante; el correo y el celular pueden ser de su madre, padre o tutor.',
  },
  {
    pregunta: '¿Puedo cambiar o cancelar mi cita?',
    respuesta:
      'Sí. Abre el enlace que te enviamos por correo y elige «Cambiar fecha u hora» o «Cancelar cita». En tu cita verás hasta qué día puedes hacerlo en línea.',
  },
  {
    pregunta: '¿Cuántas citas puedo tener?',
    respuesta:
      'Cada CURP puede tener una sola cita agendada a la vez. Si ya tienes una, cámbiala en lugar de agendar otra.',
  },
  {
    pregunta: 'No encuentro el correo de mi cita',
    respuesta: (
      <>
        Revisa la carpeta de correo no deseado. Si no está, te lo enviamos de nuevo con la CURP y el correo
        que registraste:{' '}
        <Link to="/recuperar" className={enlace}>
          recupera tu enlace
        </Link>
        .
      </>
    ),
  },
  {
    pregunta: '¿Qué pasa si no llego a mi cita?',
    respuesta:
      'Tu lugar se pierde y la cita queda como no asistida. Puedes agendar una nueva cuando quieras; si sabes que no podrás ir, cancélala para que alguien más use ese lugar.',
  },
  {
    pregunta: '¿Dónde consulto mi CURP?',
    respuesta: (
      <>
        En el sitio oficial del Registro Nacional de Población:{' '}
        <a href="https://www.gob.mx/curp/" target="_blank" rel="noopener noreferrer" className={enlace}>
          gob.mx/curp (abre en una pestaña nueva)
        </a>
        .
      </>
    ),
  },
  {
    pregunta: '¿Mis datos están protegidos?',
    respuesta: (
      <>
        Usamos tus datos solo para agendar y atender tu cita. Nadie puede ver tu cita solo con tu CURP: el
        acceso es con el enlace privado de tu correo. Consulta el{' '}
        <Link to="/aviso-de-privacidad" className={enlace}>
          aviso de privacidad
        </Link>
        .
      </>
    ),
  },
];

export function PreguntasFrecuentes() {
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
          {preguntas.map(({ pregunta, respuesta }) => (
            <details key={pregunta} className="group">
              <summary className="flex min-h-14 items-center justify-between gap-4 py-4 font-semibold text-slate-900">
                {pregunta}
                <span className="grid size-8 shrink-0 place-items-center rounded-full bg-slate-100 text-slate-700 transition-transform group-open:rotate-180">
                  <Icono nombre="chevronAbajo" className="size-4" />
                </span>
              </summary>
              <p className="pb-5 text-slate-700">{respuesta}</p>
            </details>
          ))}
        </div>
      </Contenedor>
    </section>
  );
}
