import { useState } from 'react';
import { NavLink, Navigate, Outlet, useLocation } from 'react-router';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando } from '@/components/ui/Encabezado';
import { esAdministrador, esSupervisor } from '@/features/admin/comun/permisos';
import { useSalir, useSesion } from '@/features/admin/sesion/useSesion';
import { rutaEntrar } from './consultas';

type Acceso = 'personal' | 'supervisor' | 'administrador';

const enlaces: readonly { a: string; texto: string; acceso: Acceso; exacto?: boolean }[] = [
  { a: '/admin/agenda', texto: 'Agenda', acceso: 'personal' },
  { a: '/admin/citas', texto: 'Citas', acceso: 'personal', exacto: true },
  { a: '/admin/citas/nueva', texto: 'Ventanilla', acceso: 'personal' },
  { a: '/admin/tablero', texto: 'Tablero', acceso: 'personal' },
  { a: '/admin/modulos', texto: 'Módulos', acceso: 'personal' },
  { a: '/admin/tramites', texto: 'Trámites', acceso: 'personal' },
  { a: '/admin/ofertas', texto: 'Ofertas', acceso: 'personal' },
  { a: '/admin/bloqueos', texto: 'Bloqueos', acceso: 'personal' },
  { a: '/admin/reportes', texto: 'Reportes', acceso: 'supervisor' },
  { a: '/admin/usuarios', texto: 'Usuarios', acceso: 'administrador' },
  { a: '/admin/auditoria', texto: 'Auditoría', acceso: 'administrador' },
  { a: '/admin/mi-cuenta', texto: 'Mi cuenta', acceso: 'personal' },
];

/**
 * Marco del panel con su guarda: sin sesión lleva a entrar; sin MFA, a configurarla. Ocultar menús es
 * solo comodidad: la API decide el acceso.
 */
export function LayoutAdmin() {
  const ubicacion = useLocation();
  const sesion = useSesion();
  const salir = useSalir();
  const [menuAbierto, setMenuAbierto] = useState(false);

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

  const permitido = (acceso: Acceso) =>
    acceso === 'personal' ||
    (acceso === 'supervisor' && esSupervisor(sesion.data)) ||
    (acceso === 'administrador' && esAdministrador(sesion.data));
  const visibles = enlaces.filter((e) => permitido(e.acceso));

  return (
    <div className="flex min-h-dvh flex-col bg-slate-50">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-10 focus:rounded focus:bg-white focus:p-3"
      >
        Ir al contenido
      </a>
      <header className="bg-guinda-700 text-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center justify-between gap-2">
            <p className="text-lg font-bold">Panel TIIE</p>
            <button
              type="button"
              className="inline-flex min-h-11 items-center rounded-md border border-white/40 px-3 lg:hidden"
              aria-expanded={menuAbierto}
              aria-controls="menu-panel"
              onClick={() => setMenuAbierto((abierto) => !abierto)}
            >
              Menú
            </button>
          </div>
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
          <ul
            id="menu-panel"
            className={`mx-auto max-w-6xl flex-col px-2 lg:flex lg:flex-row lg:flex-wrap lg:gap-1 ${
              menuAbierto ? 'flex' : 'hidden'
            }`}
          >
            {visibles.map((e) => (
              <li key={e.a}>
                <NavLink
                  to={e.a}
                  end={e.exacto}
                  // En celular, el menú se cierra al elegir una pantalla.
                  onClick={() => setMenuAbierto(false)}
                  className={({ isActive }) =>
                    `inline-flex min-h-11 w-full items-center px-3 ${isActive ? 'font-semibold underline underline-offset-4' : ''}`
                  }
                >
                  {e.texto}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </header>
      <main id="contenido" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
