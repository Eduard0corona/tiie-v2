import createClient, { type Middleware } from 'openapi-fetch';
import { ErrorApi, type ProblemaApi } from './errores';
import type { paths } from './esquema';

/** Toda respuesta que no es 2xx se convierte en ErrorApi. */
const convertirErrores: Middleware = {
  async onResponse({ response }) {
    if (response.ok) return response;
    const esJson = response.headers.get('content-type')?.includes('json');
    const problema = esJson
      ? ((await response
          .clone()
          .json()
          .catch(() => null)) as ProblemaApi | null)
      : null;
    throw new ErrorApi(response.status, problema, response.headers.get('Retry-After'));
  },
};

export const api = createClient<paths>({
  // Mismo origen que la SPA. Se usa el origen completo para que también funcione en las pruebas.
  baseUrl: window.location.origin,
  credentials: 'same-origin',
  headers: { 'X-Tiie-Csrf': '1' }, // protección CSRF, ver docs/07
});
api.use(convertirErrores);

/** Datos de una respuesta exitosa; el middleware ya lanzó ErrorApi si no lo fue. */
export function datos<T>(resultado: { data?: T }): T {
  if (resultado.data === undefined) throw new ErrorApi(500, null);
  return resultado.data;
}
