import { useEffect, useRef, useState } from 'react';
import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import { Marca } from '@/components/Marca';
import { Alerta } from '@/components/ui/Alerta';
import { Cargando } from '@/components/ui/Encabezado';
import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { esAdministrador, esSupervisor } from '@/features/admin/comun/permisos';
import { useSalir, useSesion } from '@/features/admin/sesion/useSesion';
import { formatearDiaSemana, hoyLocal } from '@/lib/fechas';
import { rutaEntrar } from './consultas';

type Acceso = 'personal' | 'supervisor' | 'administrador';

interface Enlace {
  a: string;
  texto: string;
  icono: NombreIcono;
  acceso: Acceso;
}

/** El menú se agrupa por tarea: lo del mostrador primero; la configuración y la administración después. */
const grupos: readonly { titulo: string; enlaces: readonly Enlace[] }[] = [
  {
    titulo: 'Operación',
    enlaces: [
      { a: '/admin/agenda', texto: 'Agenda', icono: 'calendario', acceso: 'personal' },
      { a: '/admin/citas/nueva', texto: 'Ventanilla', icono: 'ventanilla', acceso: 'personal' },
      { a: '/admin/citas', texto: 'Citas', icono: 'buscar', acceso: 'personal' },
      { a: '/admin/tablero', texto: 'Tablero', icono: 'grafica', acceso: 'personal' },
    ],
  },
  {
    titulo: 'Configuración',
    enlaces: [
      { a: '/admin/modulos', texto: 'Módulos', icono: 'sede', acceso: 'personal' },
      { a: '/admin/tramites', texto: 'Trámites', icono: 'tarjeta', acceso: 'personal' },
      { a: '/admin/ofertas', texto: 'Ofertas', icono: 'reloj', acceso: 'personal' },
      { a: '/admin/bloqueos', texto: 'Bloqueos', icono: 'prohibido', acceso: 'personal' },
    ],
  },
  {
    titulo: 'Administración',
    enlaces: [
      { a: '/admin/reportes', texto: 'Reportes', icono: 'documento', acceso: 'supervisor' },
      { a: '/admin/usuarios', texto: 'Usuarios', icono: 'grupo', acceso: 'administrador' },
      { a: '/admin/auditoria', texto: 'Auditoría', icono: 'escudo', acceso: 'administrador' },
    ],
  },
];

/** «Citas» abarca la búsqueda y el detalle, pero no la ventanilla, que tiene su propio enlace. */
function estaActivo(a: string, ruta: string): boolean {
  if (a === '/admin/citas') return ruta.startsWith('/admin/citas') && !ruta.startsWith('/admin/citas/nueva');
  return ruta === a || ruta.startsWith(`${a}/`);
}

function iniciales(nombre: string): string {
  return nombre
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p.charAt(0).toUpperCase())
    .join('');
}

/**
 * Marco del panel con su guarda: sin sesión lleva a entrar; sin MFA, a configurarla. Ocultar menús es solo
 * comodidad: la API decide el acceso. En escritorio el menú es una barra lateral fija; en celular, un panel que se
 * abre con «Menú».
 */
export function LayoutAdmin() {
  const ubicacion = useLocation();
  const sesion = useSesion();
  const salir = useSalir();
  const [menuAbierto, setMenuAbierto] = useState(false);

  useEffect(() => {
    if (!menuAbierto) return;
    const alTeclear = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMenuAbierto(false);
    };
    document.addEventListener('keydown', alTeclear);
    return () => document.removeEventListener('keydown', alTeclear);
  }, [menuAbierto]);

  if (sesion.isPending) return <Cargando texto="Cargando tu sesión…" />;
  if (sesion.error instanceof ErrorApi && sesion.error.status === 401) {
    return <Navigate to={rutaEntrar(`${ubicacion.pathname}${ubicacion.search}`)} replace />;
  }
  if (sesion.isError) {
    return (
      <main className="mx-auto max-w-xl p-4">
        <Alerta tono="error" traza={sesion.error instanceof ErrorApi ? sesion.error.traceId : null}>
          {mensajeDeError(sesion.error)}
        </Alerta>
      </main>
    );
  }
  if (!sesion.data.mfaActivo) return <Navigate to="/admin/configurar-mfa" replace />;

  const yo = sesion.data;
  const permitido = (acceso: Acceso) =>
    acceso === 'personal' ||
    (acceso === 'supervisor' && esSupervisor(yo)) ||
    (acceso === 'administrador' && esAdministrador(yo));
  const visibles = grupos
    .map((g) => ({ ...g, enlaces: g.enlaces.filter((e) => permitido(e.acceso)) }))
    .filter((g) => g.enlaces.length > 0);
  const enlace = (activo: boolean) =>
    `flex min-h-11 items-center gap-3 rounded-lg px-3 font-medium transition-colors ${
      activo
        ? 'bg-guinda-50 text-guinda-800 ring-1 ring-guinda-100'
        : 'text-slate-700 hover:bg-slate-100 hover:text-slate-950'
    }`;

  return (
    <div className="min-h-dvh bg-slate-50 lg:grid lg:grid-cols-[17rem_minmax(0,1fr)]">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-white focus:p-3 focus:font-semibold focus:shadow-elevada"
      >
        Ir al contenido
      </a>

      {menuAbierto && (
        <button
          type="button"
          aria-label="Cerrar menú"
          className="fixed inset-0 z-40 bg-slate-950/40 backdrop-blur-sm lg:hidden"
          onClick={() => setMenuAbierto(false)}
        />
      )}
      <nav
        id="menu-panel"
        aria-label="Panel"
        className={`${
          menuAbierto ? 'fixed inset-y-0 left-0 z-50 flex w-72 shadow-elevada' : 'hidden'
        } flex-col border-r border-slate-200 bg-white lg:sticky lg:top-0 lg:z-auto lg:flex lg:h-dvh lg:w-auto lg:shadow-none`}
      >
        <div className="flex h-16 shrink-0 items-center border-b border-slate-100 px-5">
          <Marca a="/admin/agenda" subtitulo="Panel del personal" etiqueta="Citas TIIE, panel del personal" />
        </div>
        <div className="flex flex-1 flex-col gap-6 overflow-y-auto px-3 py-5">
          {visibles.map((grupo) => (
            <div key={grupo.titulo}>
              <p
                id={`grupo-${grupo.titulo}`}
                className="px-3 pb-2 text-xs font-semibold tracking-wider text-slate-500 uppercase"
              >
                {grupo.titulo}
              </p>
              <ul aria-labelledby={`grupo-${grupo.titulo}`} className="flex flex-col gap-0.5">
                {grupo.enlaces.map((e) => {
                  const activo = estaActivo(e.a, ubicacion.pathname);
                  return (
                    <li key={e.a}>
                      <Link
                        to={e.a}
                        aria-current={activo ? 'page' : undefined}
                        // En celular, el menú se cierra al elegir una pantalla.
                        onClick={() => setMenuAbierto(false)}
                        className={enlace(activo)}
                      >
                        <Icono nombre={e.icono} className={activo ? 'text-guinda-700' : 'text-slate-400'} />
                        {e.texto}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </div>
        <div className="flex shrink-0 flex-col gap-1 border-t border-slate-100 p-3">
          <div className="flex items-center gap-3 px-3 py-2">
            <span
              aria-hidden="true"
              className="grid size-9 shrink-0 place-items-center rounded-full bg-guinda-700 text-sm font-bold text-white"
            >
              {iniciales(yo.nombreCompleto)}
            </span>
            <div className="min-w-0">
              <p className="truncate font-semibold text-slate-900">{yo.nombreCompleto}</p>
              <p className="truncate text-sm text-slate-600">
                {yo.rol ?? 'Sin rol'} ·{' '}
                {yo.modulos.length === 1 ? '1 módulo' : `${yo.modulos.length} módulos`}
              </p>
            </div>
          </div>
          <Link
            to="/admin/mi-cuenta"
            aria-current={estaActivo('/admin/mi-cuenta', ubicacion.pathname) ? 'page' : undefined}
            onClick={() => setMenuAbierto(false)}
            className={enlace(estaActivo('/admin/mi-cuenta', ubicacion.pathname))}
          >
            <Icono nombre="usuario" className="text-slate-400" />
            Mi cuenta
          </Link>
          {/* Botón con el aspecto de los enlaces del menú; las clases de Boton no se sobrescriben con className. */}
          <button
            type="button"
            disabled={salir.isPending}
            aria-busy={salir.isPending || undefined}
            onClick={() => salir.mutate()}
            className={`${enlace(false)} w-full disabled:cursor-wait disabled:opacity-70`}
          >
            <Icono nombre="salir" className="text-slate-400" />
            {salir.isPending ? 'Saliendo…' : 'Salir'}
          </button>
        </div>
      </nav>

      <div className="flex min-w-0 flex-col">
        <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/90 backdrop-blur-md">
          <div className="flex h-16 items-center gap-3 px-4 sm:px-6 lg:px-8">
            <button
              type="button"
              className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 font-semibold text-slate-800 lg:hidden"
              aria-expanded={menuAbierto}
              aria-controls="menu-panel"
              onClick={() => setMenuAbierto((abierto) => !abierto)}
            >
              <Icono nombre="menu" />
              Menú
            </button>
            <BusquedaGlobal />
            <p className="ml-auto hidden text-sm text-slate-600 xl:block">
              Hoy es <span className="font-semibold text-slate-900">{formatearDiaSemana(hoyLocal())}</span>
            </p>
          </div>
        </header>
        <main
          id="contenido"
          tabIndex={-1}
          className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 focus:outline-none sm:px-6 lg:px-8 lg:py-8"
        >
          <Outlet />
        </main>
      </div>
    </div>
  );
}

/**
 * Búsqueda de citas por folio o CURP desde cualquier pantalla; «/» la enfoca, como en otras herramientas de
 * trabajo. Lleva a la búsqueda de citas con el texto ya aplicado.
 */
function BusquedaGlobal() {
  const navegar = useNavigate();
  const entrada = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => {
      if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
      const destino = e.target instanceof HTMLElement ? e.target : null;
      if (destino && (destino.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(destino.tagName))) return;
      e.preventDefault();
      entrada.current?.focus();
    };
    document.addEventListener('keydown', alTeclear);
    return () => document.removeEventListener('keydown', alTeclear);
  }, []);

  return (
    <form
      role="search"
      aria-label="Buscar citas"
      className="relative min-w-0 flex-1 sm:max-w-md"
      onSubmit={(e) => {
        e.preventDefault();
        const texto = String(new FormData(e.currentTarget).get('busqueda') ?? '').trim();
        if (!texto) return;
        void navegar(`/admin/citas?busqueda=${encodeURIComponent(texto)}`);
        e.currentTarget.reset();
        entrada.current?.blur();
      }}
    >
      <label htmlFor="busqueda-global" className="sr-only">
        Buscar cita por folio o CURP
      </label>
      <Icono
        nombre="buscar"
        className="pointer-events-none absolute top-1/2 left-3 size-4.5 -translate-y-1/2 text-slate-400"
      />
      <input
        ref={entrada}
        id="busqueda-global"
        name="busqueda"
        type="search"
        autoComplete="off"
        spellCheck={false}
        aria-keyshortcuts="/"
        placeholder="Buscar folio o CURP"
        className="min-h-11 w-full rounded-lg border border-slate-300 bg-slate-50 py-2 pr-10 pl-10 text-base text-slate-900 transition-colors placeholder:text-slate-500 hover:border-slate-400 focus:bg-white"
      />
      <kbd
        aria-hidden="true"
        className="pointer-events-none absolute top-1/2 right-3 hidden -translate-y-1/2 rounded border border-slate-300 bg-white px-1.5 font-sans text-xs font-semibold text-slate-500 sm:block"
      >
        /
      </kbd>
    </form>
  );
}
