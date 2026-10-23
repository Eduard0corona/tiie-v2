import { Outlet } from 'react-router';

/** Marco de las pantallas sin sesión completa: entrar, activar, restablecer y configurar MFA. */
export function LayoutAcceso() {
  return (
    <div className="flex min-h-dvh flex-col bg-slate-100">
      <header className="bg-guinda-700 text-white">
        <p className="mx-auto max-w-md px-4 py-3 text-lg font-bold">Panel del personal · TIIE</p>
      </header>
      <main className="mx-auto w-full max-w-md flex-1 px-4 py-6">
        <div className="rounded-lg bg-white p-5 shadow-sm sm:p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
