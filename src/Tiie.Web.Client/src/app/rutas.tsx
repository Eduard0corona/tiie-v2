import { Navigate, type RouteObject } from 'react-router';
import { Cargando } from '@/components/ui/Encabezado';
import { PaginaAvisoPrivacidad } from '@/features/publico/aviso-privacidad/PaginaAvisoPrivacidad';
import { PaginaInicio } from '@/features/publico/inicio/PaginaInicio';
import { PaginaNoEncontrada } from '@/features/publico/no-encontrada/PaginaNoEncontrada';
import { LayoutPublico } from './LayoutPublico';
import { PaginaError } from './PaginaError';

/**
 * Rutas de la SPA (docs/08#rutas). Todo /admin se carga de forma perezosa: su JavaScript no llega al
 * ciudadano. /mi-cita/{token} no es una ruta de React: lo atiende el servidor.
 */
export const rutas: RouteObject[] = [
  {
    path: '/',
    errorElement: <PaginaError />,
    hydrateFallbackElement: <Cargando />,
    children: [
      {
        element: <LayoutPublico />,
        children: [
          { index: true, element: <PaginaInicio /> },
          { path: 'aviso-de-privacidad', element: <PaginaAvisoPrivacidad /> },
        ],
      },
      {
        path: 'admin',
        children: [
          {
            lazy: async () => ({
              Component: (await import('@/features/admin/sesion/LayoutAcceso')).LayoutAcceso,
            }),
            children: [
              {
                path: 'entrar',
                lazy: async () => ({
                  Component: (await import('@/features/admin/sesion/PaginaEntrar')).PaginaEntrar,
                }),
              },
              {
                path: 'activar',
                lazy: async () => ({
                  Component: (await import('@/features/admin/sesion/PaginaActivar')).PaginaActivar,
                }),
              },
              {
                path: 'restablecer',
                lazy: async () => ({
                  Component: (await import('@/features/admin/sesion/PaginaRestablecer')).PaginaRestablecer,
                }),
              },
              {
                path: 'configurar-mfa',
                lazy: async () => ({
                  Component: (await import('@/features/admin/sesion/PaginaConfigurarMfa'))
                    .PaginaConfigurarMfa,
                }),
              },
            ],
          },
          {
            lazy: async () => ({ Component: (await import('./LayoutAdmin')).LayoutAdmin }),
            children: [
              { index: true, element: <Navigate to="/admin/agenda" replace /> },
              {
                path: 'agenda',
                lazy: async () => ({
                  Component: (await import('@/features/admin/agenda/PaginaAgenda')).PaginaAgenda,
                }),
              },
              {
                path: 'mi-cuenta',
                lazy: async () => ({
                  Component: (await import('@/features/admin/cuenta/PaginaMiCuenta')).PaginaMiCuenta,
                }),
              },
            ],
          },
        ],
      },
      {
        element: <LayoutPublico />,
        children: [{ path: '*', element: <PaginaNoEncontrada /> }],
      },
    ],
  },
];
