import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { servidor } from './servidor';

// Las rutas de /admin se cargan de forma perezosa: la primera transformación en Vitest tarda más de 1 s.
configure({ asyncUtilTimeout: 5000 });

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
  cleanup();
  servidor.resetHandlers();
});
afterAll(() => servidor.close());
