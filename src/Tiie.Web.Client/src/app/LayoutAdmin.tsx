import { NavLink, Navigate, Outlet, useLocation } from 'react-router';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando } from '@/components/ui/Encabezado';
import { useSalir, useSesion } from '@/features/admin/sesion/useSesion';
import { rutaEntrar } from './consultas';

const enlaces = [
  { a: '/admin/agenda', texto: 'Agenda' },
  { a: '/admin/mi-cuenta', texto: 'Mi cuenta' },
];

/**
 * Marco del panel con su guarda: sin sesión lleva a entrar; sin MFA, a configurarla. Ocultar menús es
 * solo comodidad: la API decide el acceso.
 */
export function LayoutAdmin() {
  const ubicacion = useLocation();
  const sesion = useSesion();
  const salir = useSalir();

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

  return (
    <div className="flex min-h-dvh flex-col bg-slate-50">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-10 focus:rounded focus:bg-white focus:p-3"
      >
        Ir al contenido
      </a>
      <header className="bg-guinda-700 text-white">
        <div className="mx-auto flex max-w-5xl flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-lg font-bold">Panel TIIE</p>
          <div className="flex items-center justify-between gap-4 text-sm">
            <p>
              <span className="font-semibold">{sesion.data.nombreCompleto}</span>
              {sesion.data.rol && <span className="opacity-90"> · {sesion.data.rol}</span>}
            </p>
            <Boton
              variante="secundario"
              ocupado={salir.isPending}
              textoOcupado="Saliendo…"
              onClick={() => salir.mutate()}
              className="border-white/40 bg-transparent text-white hover:bg-guinda-800"
            >
              Salir
            </Boton>
          </div>
        </div>
        <nav aria-label="Panel" className="bg-guinda-800">
          <ul className="mx-auto flex max-w-5xl gap-1 overflow-x-auto px-2">
            {enlaces.map((e) => (
              <li key={e.a}>
                <NavLink
                  to={e.a}
                  className={({ isActive }) =>
                    `inline-flex min-h-11 items-center px-3 ${isActive ? 'font-semibold underline underline-offset-4' : ''}`
                  }
                >
                  {e.texto}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </header>
      <main id="contenido" className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
