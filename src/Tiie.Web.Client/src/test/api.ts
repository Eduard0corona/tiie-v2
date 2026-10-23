import { http, HttpResponse } from 'msw';
import type { CodigoError, ProblemaApi } from '@/api/errores';
import type { components } from '@/api/esquema';

/** Manejadores de MSW tipados con los esquemas generados del OpenAPI. */
type Esquemas = components['schemas'];

export function problema(status: number, codigo: CodigoError, extra: ProblemaApi = {}) {
  return HttpResponse.json<ProblemaApi>(
    { title: codigo, status, codigo, traceId: '00-prueba-01', ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

export function sesionPersonal(
  parcial: Partial<Esquemas['SesionPersonal']> = {},
): Esquemas['SesionPersonal'] {
  return {
    usuarioId: 7,
    usuario: 'capturista',
    nombreCompleto: 'Ana Gómez Martínez',
    rol: 'Capturista',
    modulos: [{ moduloId: 3, nombre: 'Módulo Culiacán Centro' }],
    mfaActivo: true,
    ...parcial,
  };
}

export const api = {
  yo: (respuesta: () => Esquemas['SesionPersonal'] | Response) =>
    http.get<never, never, Esquemas['SesionPersonal'] | ProblemaApi>('/api/admin/auth/yo', () => {
      const r = respuesta();
      return r instanceof Response ? r : HttpResponse.json(r);
    }),

  entrar: (respuesta: (cuerpo: Esquemas['SolicitudEntrar']) => Esquemas['RespuestaEntrar'] | Response) =>
    http.post<never, Esquemas['SolicitudEntrar'], Esquemas['RespuestaEntrar'] | ProblemaApi>(
      '/api/admin/auth/entrar',
      async ({ request }) => {
        const r = respuesta(await request.json());
        return r instanceof Response ? r : HttpResponse.json(r);
      },
    ),

  entrarMfa: (respuesta: (cuerpo: Esquemas['SolicitudCodigoMfa']) => Response | null) =>
    http.post<never, Esquemas['SolicitudCodigoMfa'], ProblemaApi>(
      '/api/admin/auth/entrar/mfa',
      async ({ request }) => {
        return respuesta(await request.json()) ?? new HttpResponse(null, { status: 204 });
      },
    ),

  salir: () => http.post('/api/admin/auth/salir', () => new HttpResponse(null, { status: 204 })),

  claveMfa: (clave: Esquemas['ClaveAutenticador']) =>
    http.get<never, never, Esquemas['ClaveAutenticador']>('/api/admin/auth/mfa', () =>
      HttpResponse.json(clave),
    ),

  activarMfa: (
    respuesta: (cuerpo: Esquemas['SolicitudActivarMfa']) => Esquemas['RespuestaMfaActivada'] | Response,
  ) =>
    http.post<never, Esquemas['SolicitudActivarMfa'], Esquemas['RespuestaMfaActivada'] | ProblemaApi>(
      '/api/admin/auth/mfa',
      async ({ request }) => {
        const r = respuesta(await request.json());
        return r instanceof Response ? r : HttpResponse.json(r);
      },
    ),

  olvide: () =>
    http.post<never, Esquemas['SolicitudOlvideContrasena']>(
      '/api/admin/auth/olvide-contrasena',
      () => new HttpResponse(null, { status: 202 }),
    ),

  restablecimiento: (respuesta: () => Response | null) =>
    http.post<never, Esquemas['SolicitudContrasenaConToken'], ProblemaApi>(
      '/api/admin/auth/restablecimiento',
      () => respuesta() ?? new HttpResponse(null, { status: 204 }),
    ),
};
