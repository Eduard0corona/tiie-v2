import { QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
// En Vitest, react-router/dom resuelve otra copia de react-router y su contexto no coincide.
import { RouterProvider, createMemoryRouter } from 'react-router';
import { crearClienteConsultas } from '@/app/consultas';
import { rutas } from '@/app/rutas';

/** Dibuja la SPA completa en una ruta, con un router en memoria y una caché nueva. */
export function renderizarRuta(ruta: string) {
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] });
  const cliente = crearClienteConsultas(
    (destino) => void router.navigate(destino),
    () => `${router.state.location.pathname}${router.state.location.search}`,
  );
  cliente.setDefaultOptions({ queries: { retry: false } });
  const usuario = userEvent.setup();
  render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { router, cliente, usuario };
}
