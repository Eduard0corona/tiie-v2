import { setupServer } from 'msw/node';

/** API simulada con MSW; cada prueba registra sus manejadores con `servidor.use`. */
export const servidor = setupServer();
