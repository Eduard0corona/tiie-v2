import { QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { crearClienteConsultas } from './app/consultas';
import { rutas } from './app/rutas';
import './styles/index.css';

const router = createBrowserRouter(rutas);
const clienteConsultas = crearClienteConsultas(
  (ruta) => void router.navigate(ruta),
  () => `${router.state.location.pathname}${router.state.location.search}`,
);

const raiz = document.getElementById('raiz');
if (!raiz) throw new Error('Falta el elemento #raiz en index.html.');

createRoot(raiz).render(
  <StrictMode>
    <QueryClientProvider client={clienteConsultas}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
