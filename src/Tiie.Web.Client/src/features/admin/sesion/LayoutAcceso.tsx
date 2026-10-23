import { Outlet } from 'react-router';
import { Marca } from '@/components/Marca';
import { Icono, type NombreIcono } from '@/components/ui/Icono';

const funciones: readonly { icono: NombreIcono; texto: string }[] = [
  { icono: 'calendario', texto: 'Agenda del día y registro de asistencia' },
  { icono: 'ventanilla', texto: 'Citas en ventanilla, cambios y cancelaciones' },
  { icono: 'reloj', texto: 'Horarios, bloqueos y reportes de tus módulos' },
];

/**
 * Marco de las pantallas sin sesión completa: entrar, activar, restablecer y configurar MFA. En escritorio, a la
 * izquierda, qué es el panel; a la derecha, el formulario.
 */
export function LayoutAcceso() {
  return (
    <div className="grid min-h-dvh bg-slate-50 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
      <aside className="relative hidden overflow-hidden bg-guinda-800 p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0">
          <div className="absolute -top-24 -left-24 size-96 rounded-full bg-guinda-600/40 blur-3xl" />
          <div className="absolute -right-24 -bottom-32 size-96 rounded-full bg-dorado-400/20 blur-3xl" />
        </div>
        <p className="relative text-sm font-semibold tracking-widest text-guinda-100 uppercase">
          Gobierno del Estado de Sinaloa
        </p>
        <div className="relative flex max-w-md flex-col gap-6">
          <p className="text-4xl leading-tight font-bold tracking-tight">Panel del personal de la TIIE</p>
          <ul className="flex flex-col gap-4 text-lg text-guinda-50">
            {funciones.map((f) => (
              <li key={f.texto} className="flex items-center gap-3">
                <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-white/10 ring-1 ring-white/20">
                  <Icono nombre={f.icono} className="text-dorado-200" />
                </span>
                {f.texto}
              </li>
            ))}
          </ul>
        </div>
        <p className="relative flex items-center gap-2 text-sm text-guinda-100">
          <Icono nombre="escudo" className="size-4 text-dorado-200" />
          Acceso solo para personal autorizado, con verificación en dos pasos.
        </p>
      </aside>
      <div className="flex flex-col">
        <header className="flex h-16 items-center border-b border-slate-200 bg-white px-4 sm:px-6 lg:border-0 lg:bg-transparent lg:px-12">
          <Marca a="/admin/entrar" subtitulo="Panel del personal" etiqueta="Citas TIIE, panel del personal" />
        </header>
        <main className="flex flex-1 items-start justify-center px-4 py-8 sm:items-center sm:py-12">
          <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-tarjeta sm:p-8">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
