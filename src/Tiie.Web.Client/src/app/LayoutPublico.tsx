import { Link, Outlet } from 'react-router';

export function LayoutPublico() {
  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-10 focus:rounded focus:bg-white focus:p-3"
      >
        Ir al contenido
      </a>
      <header className="bg-guinda-700 text-white">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-4 py-3">
          <Link to="/" className="flex min-h-11 items-center text-lg font-bold">
            Citas TIIE · Sinaloa
          </Link>
        </div>
      </header>
      <main id="contenido" className="mx-auto w-full max-w-3xl flex-1 px-4 py-6">
        <Outlet />
      </main>
      <footer className="border-t border-slate-200 bg-white">
        <div className="mx-auto flex max-w-3xl flex-col gap-1 px-4 py-4 text-sm text-slate-600 sm:flex-row sm:justify-between">
          <p>Tarjeta Inteligente de Identidad Estudiantil · Gobierno del Estado de Sinaloa</p>
          <Link to="/aviso-de-privacidad" className="text-guinda-700 underline underline-offset-4">
            Aviso de privacidad
          </Link>
        </div>
      </footer>
    </div>
  );
}
