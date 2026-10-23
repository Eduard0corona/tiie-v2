import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { servidor } from './servidor';

// Las rutas de /admin se cargan de forma perezosa: con todos los archivos de prueba en paralelo, la primera
// carga de una pantalla del panel tarda varios segundos.
configure({ asyncUtilTimeout: 10_000 });

// MSW intercepta antes de que se importe cualquier módulo de la prueba: openapi-fetch guarda fetch y Request
// al crear el cliente, y el sitio público lo importa de forma estática.
servidor.listen({ onUnhandledFrame: 'error' });

afterEach(() => {
  cleanup();
  servidor.resetHandlers();
});
afterAll(() => servidor.close());
