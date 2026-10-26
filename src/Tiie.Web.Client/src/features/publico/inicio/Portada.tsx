import { Link } from 'react-router';
import type { components } from '@/api/esquema';
import { estiloBoton } from '@/components/ui/Boton';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono, type NombreIcono } from '@/components/ui/Icono';

const beneficios: readonly { icono: NombreIcono; texto: string }[] = [
  { icono: 'usuario', texto: 'Sin crear una cuenta' },
  { icono: 'correo', texto: 'Comprobante en tu correo' },
  { icono: 'cambiar', texto: 'Cambia o cancela en línea' },
];

export type EncabezadoPortada = Pick<
  components['schemas']['PortadaPublica'],
  'etiqueta' | 'titulo' | 'tituloDestacado' | 'descripcion'
>;

/**
 * Primera pantalla: qué se hace aquí, la acción principal y el camino para quien ya tiene cita. Los textos los
 * edita el administrador; mientras llegan, unas barras ocupan su lugar para que la página no salte.
 */
export function Portada({ encabezado }: { encabezado: EncabezadoPortada | null }) {
  return (
    <section
      aria-labelledby="titulo-inicio"
      className="relative overflow-hidden bg-linear-to-b from-guinda-50 via-white to-slate-50"
    >
      <div aria-hidden="true" className="pointer-events-none absolute inset-0">
        <div className="absolute -top-32 -right-24 size-96 rounded-full bg-guinda-100/70 blur-3xl" />
        <div className="absolute top-1/2 -left-32 size-80 rounded-full bg-dorado-100/70 blur-3xl" />
      </div>
      <Contenedor
        ancho="amplio"
        className="relative grid items-center gap-12 py-12 sm:py-16 lg:grid-cols-[1.15fr_0.85fr] lg:py-24"
      >
        <div className="flex flex-col">
          {encabezado ? <TextosPortada encabezado={encabezado} /> : <TextosCargando />}
          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <Link to="/agendar" className={estiloBoton({ tamano: 'grande' })}>
              Agendar cita
              <Icono nombre="flecha" />
            </Link>
            <Link to="/mi-cita" className={estiloBoton({ variante: 'secundario', tamano: 'grande' })}>
              Ya tengo una cita
            </Link>
          </div>
          <ul className="mt-10 grid gap-3 text-sm font-medium text-slate-700 sm:grid-cols-3">
            {beneficios.map((b) => (
              <li key={b.texto} className="flex items-center gap-2.5">
                <span className="grid size-9 shrink-0 place-items-center rounded-full bg-white text-guinda-700 shadow-sm ring-1 ring-slate-200">
                  <Icono nombre={b.icono} className="size-4.5" />
                </span>
                {b.texto}
              </li>
            ))}
          </ul>
        </div>
        <IlustracionTarjeta />
      </Contenedor>
    </section>
  );
}

function TextosPortada({ encabezado }: { encabezado: EncabezadoPortada }) {
  return (
    <>
      {encabezado.etiqueta && (
        <p className="inline-flex items-center gap-2 self-start rounded-full border border-guinda-200 bg-white/80 px-3 py-1 text-sm font-semibold text-guinda-800 shadow-sm">
          <Icono nombre="sede" className="size-4" />
          {encabezado.etiqueta}
        </p>
      )}
      <h1
        id="titulo-inicio"
        className="mt-6 text-4xl leading-[1.1] font-extrabold tracking-tight text-slate-950 sm:text-5xl lg:text-[3.4rem]"
      >
        {encabezado.titulo}
        {encabezado.tituloDestacado && (
          <>
            {' '}
            <span className="text-guinda-700">{encabezado.tituloDestacado}</span>
          </>
        )}
      </h1>
      {encabezado.descripcion && (
        <p className="mt-6 max-w-xl text-lg whitespace-pre-line text-slate-700 sm:text-xl">
          {encabezado.descripcion}
        </p>
      )}
    </>
  );
}

/** Mismo alto aproximado que los textos, para que los botones no se muevan al llegar. */
function TextosCargando() {
  return (
    <div role="status" className="flex flex-col">
      <span className="sr-only">Cargando la portada…</span>
      <div aria-hidden="true" className="h-8 w-72 max-w-full animate-pulse rounded-full bg-guinda-100" />
      <div aria-hidden="true" className="mt-6 flex flex-col gap-3">
        <div className="h-10 w-11/12 animate-pulse rounded-xl bg-slate-200/80 sm:h-12" />
        <div className="h-10 w-full animate-pulse rounded-xl bg-slate-200/80 sm:h-12" />
        <div className="h-10 w-2/3 animate-pulse rounded-xl bg-slate-200/80 sm:h-12" />
      </div>
      <div aria-hidden="true" className="mt-6 flex max-w-xl flex-col gap-2">
        <div className="h-6 w-full animate-pulse rounded-lg bg-slate-200/70" />
        <div className="h-6 w-5/6 animate-pulse rounded-lg bg-slate-200/70" />
      </div>
    </div>
  );
}

/**
 * Tarjeta estilizada, no la credencial real: da identidad a la portada sin descargar imágenes. Es decorativa
 * y se oculta a los lectores de pantalla.
 */
function IlustracionTarjeta() {
  return (
    <div aria-hidden="true" className="relative mx-auto w-full max-w-sm py-6 sm:max-w-md">
      <div className="-rotate-6 rounded-[1.4rem] shadow-elevada transition-transform duration-500 hover:rotate-0">
        <svg viewBox="0 0 360 227" className="block h-auto w-full" focusable="false">
          <defs>
            <linearGradient id="tiie-fondo" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor="#93294a" />
              <stop offset="1" stopColor="#4a1123" />
            </linearGradient>
            <linearGradient id="tiie-chip" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor="#f3eadb" />
              <stop offset="1" stopColor="#bc955c" />
            </linearGradient>
          </defs>
          <rect width="360" height="227" rx="20" fill="url(#tiie-fondo)" />
          <circle cx="330" cy="-20" r="130" fill="#fff" opacity="0.06" />
          <circle cx="300" cy="250" r="95" fill="#bc955c" opacity="0.2" />
          <path d="M0 175c90-35 190 25 360-25v77H0z" fill="#fff" opacity="0.05" />
          <text x="26" y="48" fill="#fff" fontSize="30" fontWeight="800" letterSpacing="3">
            TIIE
          </text>
          <text x="26" y="68" fill="#fff" opacity="0.8" fontSize="9.5" fontWeight="600" letterSpacing="1">
            TARJETA INTELIGENTE DE IDENTIDAD ESTUDIANTIL
          </text>
          <rect x="26" y="90" width="46" height="36" rx="7" fill="url(#tiie-chip)" />
          <path
            d="M26 102h14M58 102h14M26 114h14M58 114h14M40 90v36M58 90v36"
            stroke="#a57f48"
            strokeWidth="1.5"
          />
          <path
            d="M86 98a14 14 0 0 1 0 20M93 93a22 22 0 0 1 0 30"
            fill="none"
            stroke="#fff"
            strokeWidth="2.5"
            strokeLinecap="round"
            opacity="0.7"
          />
          <rect x="262" y="84" width="74" height="92" rx="12" fill="#fff" opacity="0.14" />
          <circle cx="299" cy="116" r="15" fill="#fff" opacity="0.55" />
          <path d="M276 165a23 21 0 0 1 46 0z" fill="#fff" opacity="0.55" />
          <rect x="26" y="146" width="150" height="11" rx="5.5" fill="#fff" opacity="0.9" />
          <rect x="26" y="165" width="104" height="8" rx="4" fill="#fff" opacity="0.45" />
          <text x="26" y="206" fill="#e6d3b3" fontSize="11" fontWeight="700" letterSpacing="4">
            SINALOA
          </text>
        </svg>
      </div>
      <div className="absolute top-0 -right-1 flex items-center gap-2 rounded-full bg-white px-3.5 py-2 text-sm font-semibold text-slate-800 shadow-elevada sm:-right-6">
        <span className="grid size-6 place-items-center rounded-full bg-emerald-600 text-white">
          <Icono nombre="check" className="size-4" />
        </span>
        Cita confirmada
      </div>
      <div className="absolute bottom-0 -left-1 flex items-center gap-2 rounded-full bg-white px-3.5 py-2 text-sm font-semibold text-slate-800 shadow-elevada sm:-left-6">
        <span className="grid size-6 place-items-center rounded-full bg-guinda-100 text-guinda-700">
          <Icono nombre="calendario" className="size-4" />
        </span>
        Tú eliges día y hora
      </div>
    </div>
  );
}
