import { useQuery } from '@tanstack/react-query';
import { api, datos } from '@/api/cliente';

/** Claves de consulta del panel: `['admin', recurso, filtros]` (docs/08#estado-del-servidor). */
export const clavesAdmin = {
  catalogos: ['admin', 'catalogos'] as const,
  modulos: (activo?: boolean) => ['admin', 'modulos', { activo }] as const,
  modulo: (moduloId: number) => ['admin', 'modulo', moduloId] as const,
  tiposTramite: ['admin', 'tipos-tramite'] as const,
  tipoTramite: (id: number) => ['admin', 'tipo-tramite', id] as const,
  ofertas: (moduloId?: number) => ['admin', 'ofertas', { moduloId }] as const,
  oferta: (ofertaId: number) => ['admin', 'oferta', ofertaId] as const,
  plantilla: (ofertaId: number) => ['admin', 'plantilla', ofertaId] as const,
  cupos: (ofertaId: number, desde: string, hasta: string) =>
    ['admin', 'cupos', ofertaId, desde, hasta] as const,
  agenda: (moduloId: number, fecha: string) => ['admin', 'agenda', moduloId, fecha] as const,
  citas: (filtros: object) => ['admin', 'citas', filtros] as const,
  cita: (citaId: number) => ['admin', 'cita', citaId] as const,
  bloqueos: (filtros: object) => ['admin', 'bloqueos', filtros] as const,
  usuarios: (filtros: object) => ['admin', 'usuarios', filtros] as const,
  usuario: (usuarioId: number) => ['admin', 'usuario', usuarioId] as const,
  reporte: (filtros: object) => ['admin', 'reporte', filtros] as const,
  tablero: (moduloId?: number) => ['admin', 'tablero', { moduloId }] as const,
  auditoria: (filtros: object) => ['admin', 'auditoria', filtros] as const,
  procesos: ['admin', 'procesos'] as const,
};

const CINCO_MINUTOS = 5 * 60_000;

/** Municipios, niveles escolares y roles para los formularios. */
export function useCatalogosAdmin() {
  return useQuery({
    queryKey: clavesAdmin.catalogos,
    queryFn: async () => datos(await api.GET('/api/admin/catalogos')),
    staleTime: CINCO_MINUTOS,
  });
}

/** Módulos que el usuario puede ver; el administrador, todos. */
export function useModulosAdmin(activo?: boolean) {
  return useQuery({
    queryKey: clavesAdmin.modulos(activo),
    queryFn: async () =>
      datos(
        await api.GET('/api/admin/modulos', { params: { query: activo === undefined ? {} : { activo } } }),
      ),
  });
}

export function useTiposTramiteAdmin() {
  return useQuery({
    queryKey: clavesAdmin.tiposTramite,
    queryFn: async () => datos(await api.GET('/api/admin/tipos-tramite')),
  });
}

/** Ofertas de un módulo, o de todos los módulos del usuario. */
export function useOfertasAdmin(moduloId?: number | null, habilitada = true) {
  return useQuery({
    queryKey: clavesAdmin.ofertas(moduloId ?? undefined),
    queryFn: async () =>
      datos(await api.GET('/api/admin/ofertas', { params: { query: moduloId ? { moduloId } : {} } })),
    enabled: habilitada,
  });
}
