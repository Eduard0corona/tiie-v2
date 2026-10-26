import { useEffect } from 'react';
import { Link, useLocation } from 'react-router';
import { estiloBoton } from '@/components/ui/Boton';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { usePortada } from '../comun/usePublico';
import { AvisoPortada } from './AvisoPortada';
import { EncabezadoSeccion } from './EncabezadoSeccion';
import { Portada, type EncabezadoPortada } from './Portada';
import { PreguntasFrecuentes } from './PreguntasFrecuentes';
import { SeccionSedes } from './SeccionSedes';
import { SeccionTramites } from './SeccionTramites';

/** Si la API no responde, la portada conserva su mensaje y sus acciones. */
const ENCABEZADO_RESPALDO: EncabezadoPortada = {
  etiqueta: '',
  titulo: 'Agenda tu cita para la',
  tituloDestacado: 'Tarjeta Inteligente de Identidad Estudiantil',
  descripcion: '',
};

/**
 * Portada del sitio (RF-01): qué se hace aquí, cómo, qué llevar, dónde y las dudas comunes. Cada sección
 * termina en la misma acción: agendar. El encabezado, el aviso y las preguntas los edita el administrador.
 */
export function PaginaInicio() {
  const portada = usePortada();
  const { hash } = useLocation();
  // React Router no desplaza a los anclas al navegar dentro de la SPA (por ejemplo, «Sedes» del encabezado).
  useEffect(() => {
    if (hash) document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView?.();
  }, [hash]);

  return (
    <>
      {portada.data?.aviso && <AvisoPortada aviso={portada.data.aviso} />}
      <Portada encabezado={portada.data ?? (portada.isError ? ENCABEZADO_RESPALDO : null)} />
      <ComoFunciona />
      <SeccionTramites />
      <SeccionSedes />
      <PreguntasFrecuentes preguntas={portada.data?.preguntas} error={portada.error ?? undefined} />
      <LlamadaFinal />
    </>
  );
}

const pasos: readonly { icono: NombreIcono; titulo: string; texto: string }[] = [
  {
    icono: 'sede',
    titulo: 'Elige sede y trámite',
    texto: 'Busca el módulo que te quede más cerca y el trámite que necesitas.',
  },
  {
    icono: 'calendario',
    titulo: 'Escoge día y hora',
    texto: 'El calendario te muestra qué días tienen lugares y cuántos quedan en cada hora.',
  },
  {
    icono: 'usuario',
    titulo: 'Escribe tus datos',
    texto: 'Solo la CURP, el nombre, un celular y un correo. Nada de documentos en línea.',
  },
  {
    icono: 'documento',
    titulo: 'Acude con tus requisitos',
    texto: 'Recibes el comprobante por correo. Llega a tu hora con lo que pide tu trámite.',
  },
];

function ComoFunciona() {
  return (
    <section id="como-funciona" aria-labelledby="titulo-como-funciona" className="py-16 sm:py-20">
      <Contenedor ancho="amplio" className="flex flex-col gap-10">
        <EncabezadoSeccion
          id="titulo-como-funciona"
          antetitulo="Así de fácil"
          titulo="Agenda en cuatro pasos"
          descripcion="Te toma unos minutos y no necesitas imprimir nada para agendar."
        />
        <ol className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {pasos.map((paso, i) => (
            <li
              key={paso.titulo}
              // El número grande es decorativo (la lista ya está numerada): va en CSS para que no sea texto.
              data-numero={i + 1}
              className="relative flex flex-col gap-3 overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 shadow-tarjeta before:absolute before:top-3 before:right-5 before:text-6xl before:font-black before:text-slate-100 before:content-[attr(data-numero)]"
            >
              <span className="relative grid size-12 place-items-center rounded-xl bg-guinda-700 text-white shadow-sm">
                <Icono nombre={paso.icono} className="size-6" />
              </span>
              <h3 className="relative text-lg font-bold text-slate-950">{paso.titulo}</h3>
              <p className="relative text-slate-600">{paso.texto}</p>
            </li>
          ))}
        </ol>
        <div>
          <Link to="/agendar" className={estiloBoton({ variante: 'secundario' })}>
            Empezar ahora
            <Icono nombre="flecha" />
          </Link>
        </div>
      </Contenedor>
    </section>
  );
}

function LlamadaFinal() {
  return (
    <section aria-labelledby="titulo-llamada-final" className="pb-16 sm:pb-20">
      <Contenedor ancho="amplio">
        <div className="relative overflow-hidden rounded-3xl bg-guinda-800 px-6 py-12 text-center text-white sm:px-12 sm:py-16">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0">
            <div className="absolute -top-24 -left-16 size-72 rounded-full bg-guinda-600/50 blur-2xl" />
            <div className="absolute -right-20 -bottom-28 size-80 rounded-full bg-dorado-400/25 blur-2xl" />
          </div>
          <div className="relative mx-auto flex max-w-2xl flex-col items-center gap-4">
            <h2 id="titulo-llamada-final" className="text-3xl font-bold tracking-tight sm:text-4xl">
              ¿Listo para tu trámite?
            </h2>
            <p className="text-lg text-guinda-100">
              Agenda en unos minutos y recibe tu comprobante por correo.
            </p>
            <Link to="/agendar" className={`${estiloBoton({ variante: 'claro', tamano: 'grande' })} mt-4`}>
              Agendar mi cita
              <Icono nombre="flecha" />
            </Link>
          </div>
        </div>
      </Contenedor>
    </section>
  );
}
