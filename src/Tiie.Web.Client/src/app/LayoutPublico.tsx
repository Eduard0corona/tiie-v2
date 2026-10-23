import { Link, Outlet, useLocation } from 'react-router';
import { estiloBoton } from '@/components/ui/Boton';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';
import { Marca } from '@/components/Marca';

const secciones = [
  { a: '/#tramites', texto: 'Trámites' },
  { a: '/#sedes', texto: 'Sedes' },
  { a: '/#preguntas', texto: 'Preguntas' },
] as const;

const enlaceNavegacion =
  'inline-flex min-h-11 items-center rounded-lg px-3 font-medium text-slate-700 transition-colors hover:bg-slate-100 hover:text-slate-950';

/**
 * Marco del sitio público. La franja superior identifica el sitio oficial (contra copias que piden datos);
 * el encabezado es fijo y siempre ofrece «Agendar» y «Mi cita», salvo «Agendar» dentro del asistente.
 */
export function LayoutPublico() {
  const { pathname } = useLocation();
  const enAsistente = pathname.startsWith('/agendar');

  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-white focus:p-3 focus:font-semibold focus:shadow-elevada"
      >
        Ir al contenido
      </a>
      <div className="bg-guinda-900 text-guinda-50">
        <Contenedor ancho="amplio" className="flex min-h-8 items-center gap-2 py-1 text-xs sm:text-sm">
          <Icono nombre="escudo" className="size-4 text-dorado-300" />
          <span>Sitio oficial del Gobierno del Estado de Sinaloa</span>
        </Contenedor>
      </div>
      <header className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/90 backdrop-blur-md">
        <Contenedor ancho="amplio" className="flex h-16 items-center justify-between gap-3">
          <Marca />
          <nav aria-label="Principal" className="flex items-center gap-1">
            <ul className="hidden items-center gap-1 md:flex">
              {secciones.map((s) => (
                <li key={s.a}>
                  <Link to={s.a} className={enlaceNavegacion}>
                    {s.texto}
                  </Link>
                </li>
              ))}
            </ul>
            <Link to="/mi-cita" className={enlaceNavegacion}>
              Mi cita
            </Link>
            {!enAsistente && (
              <Link to="/agendar" className={`${estiloBoton()} ml-1`}>
                Agendar
              </Link>
            )}
          </nav>
        </Contenedor>
      </header>
      <main id="contenido" tabIndex={-1} className="flex-1 focus:outline-none">
        <Outlet />
      </main>
      <PiePagina />
    </div>
  );
}

function PiePagina() {
  const enlace = 'inline-flex min-h-11 items-center text-slate-700 hover:text-guinda-800 hover:underline';
  return (
    <footer className="mt-auto border-t border-slate-200 bg-white">
      <Contenedor ancho="amplio" className="grid gap-8 py-10 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-3 lg:col-span-2">
          <Marca />
          <p className="max-w-sm text-sm text-slate-600">
            Agenda en línea para el trámite de la Tarjeta Inteligente de Identidad Estudiantil en los módulos
            de atención de Sinaloa.
          </p>
        </div>
        <nav aria-labelledby="pie-citas">
          <h2 id="pie-citas" className="text-sm font-semibold tracking-wide text-slate-900 uppercase">
            Citas
          </h2>
          <ul className="mt-2">
            <li>
              <Link to="/agendar" className={enlace}>
                Agendar una cita
              </Link>
            </li>
            <li>
              <Link to="/mi-cita" className={enlace}>
                Consultar mi cita
              </Link>
            </li>
            <li>
              <Link to="/recuperar" className={enlace}>
                Recuperar el enlace de mi cita
              </Link>
            </li>
          </ul>
        </nav>
        <nav aria-labelledby="pie-informacion">
          <h2 id="pie-informacion" className="text-sm font-semibold tracking-wide text-slate-900 uppercase">
            Información
          </h2>
          <ul className="mt-2">
            <li>
              <Link to="/#tramites" className={enlace}>
                Trámites y requisitos
              </Link>
            </li>
            <li>
              <Link to="/#sedes" className={enlace}>
                Sedes y horarios
              </Link>
            </li>
            <li>
              <Link to="/#preguntas" className={enlace}>
                Preguntas frecuentes
              </Link>
            </li>
            <li>
              <Link to="/aviso-de-privacidad" className={enlace}>
                Aviso de privacidad
              </Link>
            </li>
          </ul>
        </nav>
      </Contenedor>
      <div className="border-t border-slate-100">
        <Contenedor
          ancho="amplio"
          className="flex flex-col gap-1 py-5 text-sm text-slate-600 sm:flex-row sm:justify-between"
        >
          <p>Gobierno del Estado de Sinaloa</p>
          <p>Tarjeta Inteligente de Identidad Estudiantil (TIIE)</p>
        </Contenedor>
      </div>
    </footer>
  );
}
