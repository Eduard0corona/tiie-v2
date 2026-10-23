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
          // El asistente, Mi cita y la recuperación se cargan al entrar: el inicio queda ligero.
          {
            path: 'agendar',
            lazy: async () => ({
              Component: (await import('@/features/publico/agendar/PaginaAgendar')).PaginaAgendar,
            }),
          },
          {
            path: 'mi-cita',
            lazy: async () => ({
              Component: (await import('@/features/publico/mi-cita/PaginaMiCita')).PaginaMiCita,
            }),
          },
          {
            path: 'recuperar',
            lazy: async () => ({
              Component: (await import('@/features/publico/recuperar/PaginaRecuperar')).PaginaRecuperar,
            }),
          },
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
                path: 'citas',
                lazy: async () => ({
                  Component: (await import('@/features/admin/citas/PaginaCitas')).PaginaCitas,
                }),
              },
              {
                path: 'citas/nueva',
                lazy: async () => ({
                  Component: (await import('@/features/admin/citas/PaginaVentanilla')).PaginaVentanilla,
                }),
              },
              {
                path: 'citas/:citaId',
                lazy: async () => ({
                  Component: (await import('@/features/admin/citas/PaginaDetalleCita')).PaginaDetalleCita,
                }),
              },
              {
                path: 'modulos',
                lazy: async () => ({
                  Component: (await import('@/features/admin/modulos/PaginaModulos')).PaginaModulos,
                }),
              },
              {
                // También atiende «nuevo».
                path: 'modulos/:moduloId',
                lazy: async () => ({
                  Component: (await import('@/features/admin/modulos/PaginaModulo')).PaginaModulo,
                }),
              },
              {
                path: 'tramites',
                lazy: async () => ({
                  Component: (await import('@/features/admin/tramites/PaginaTramites')).PaginaTramites,
                }),
              },
              {
                path: 'tramites/:tipoTramiteId',
                lazy: async () => ({
                  Component: (await import('@/features/admin/tramites/PaginaTramite')).PaginaTramite,
                }),
              },
              {
                path: 'ofertas',
                lazy: async () => ({
                  Component: (await import('@/features/admin/ofertas/PaginaOfertas')).PaginaOfertas,
                }),
              },
              {
                path: 'ofertas/:ofertaId',
                lazy: async () => ({
                  Component: (await import('@/features/admin/ofertas/PaginaOferta')).PaginaOferta,
                }),
              },
              {
                path: 'bloqueos',
                lazy: async () => ({
                  Component: (await import('@/features/admin/bloqueos/PaginaBloqueos')).PaginaBloqueos,
                }),
              },
              {
                path: 'usuarios',
                lazy: async () => ({
                  Component: (await import('@/features/admin/usuarios/PaginaUsuarios')).PaginaUsuarios,
                }),
              },
              {
                path: 'usuarios/:usuarioId',
                lazy: async () => ({
                  Component: (await import('@/features/admin/usuarios/PaginaUsuario')).PaginaUsuario,
                }),
              },
              {
                path: 'reportes',
                lazy: async () => ({
                  Component: (await import('@/features/admin/reportes/PaginaReportes')).PaginaReportes,
                }),
              },
              {
                path: 'tablero',
                lazy: async () => ({
                  Component: (await import('@/features/admin/tablero/PaginaTablero')).PaginaTablero,
                }),
              },
              {
                path: 'auditoria',
                lazy: async () => ({
                  Component: (await import('@/features/admin/auditoria/PaginaAuditoria')).PaginaAuditoria,
                }),
              },
              {
                path: 'mi-cuenta',
                lazy: async () => ({
                  Component: (await import('@/features/admin/cuenta/PaginaMiCuenta')).PaginaMiCuenta,
                }),
              },
              {
                path: '*',
                lazy: async () => ({
                  Component: (await import('@/features/admin/comun/PaginaNoEncontradaAdmin'))
                    .PaginaNoEncontradaAdmin,
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
