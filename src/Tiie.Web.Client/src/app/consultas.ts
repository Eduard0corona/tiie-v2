import { QueryCache, QueryClient } from '@tanstack/react-query';
import { ErrorApi } from '@/api/errores';

/** Claves de consulta compartidas (docs/08#estado-del-servidor). */
export const claves = {
  admin: ['admin'] as const,
  sesion: ['admin', 'sesion'] as const,
  claveMfa: ['admin', 'mfa', 'clave'] as const,
};

/** Ruta interna del panel a la que se puede volver después de entrar; evita redirecciones abiertas. */
export function rutaDeRegreso(volver: string | null | undefined): string {
  if (
    volver &&
    volver.startsWith('/admin') &&
    !volver.startsWith('//') &&
    !volver.startsWith('/admin/entrar')
  ) {
    return volver;
  }
  return '/admin/agenda';
}

export function rutaEntrar(volver: string): string {
  return `/admin/entrar?volver=${encodeURIComponent(volver)}`;
}

/**
 * Un 401 en una consulta del panel borra su caché y lleva a entrar. La sesión (`['admin','sesion']`) la
 * maneja la guarda de LayoutAdmin.
 */
export function crearClienteConsultas(
  navegar: (ruta: string) => void,
  rutaActual: () => string,
): QueryClient {
  const cliente: QueryClient = new QueryClient({
    queryCache: new QueryCache({
      onError: (error, consulta) => {
        const [grupo, recurso] = consulta.queryKey;
        if (error instanceof ErrorApi && error.status === 401 && grupo === 'admin' && recurso !== 'sesion') {
          cliente.removeQueries({ queryKey: claves.admin });
          navegar(rutaEntrar(rutaActual()));
        }
      },
    }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Los errores 4xx no se reintentan: la respuesta no va a cambiar.
        retry: (intentos, error) => !(error instanceof ErrorApi && error.status < 500) && intentos < 2,
      },
    },
  });
  return cliente;
}
