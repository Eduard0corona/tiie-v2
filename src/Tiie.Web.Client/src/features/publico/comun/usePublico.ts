import { useQuery } from '@tanstack/react-query';
import { api, datos } from '@/api/cliente';

/** Claves de consulta del sitio público (docs/08#estado-del-servidor). */
export const clavesPublicas = {
  todo: ['publico'] as const,
  configuracion: ['publico', 'configuracion'] as const,
  portada: ['publico', 'portada'] as const,
  tiposTramite: ['publico', 'tipos-tramite'] as const,
  municipios: ['publico', 'municipios'] as const,
  modulos: ['publico', 'modulos'] as const,
  modulosDe: (municipioId: number) => ['publico', 'modulos', municipioId] as const,
  ofertas: (moduloId: number) => ['publico', 'ofertas', moduloId] as const,
  disponibilidad: (ofertaId: number) => ['publico', 'disponibilidad', ofertaId] as const,
  cupos: (ofertaId: number, fecha: string) => ['publico', 'cupos', ofertaId, fecha] as const,
};

const CINCO_MINUTOS = 5 * 60_000;

export function useConfiguracion() {
  return useQuery({
    queryKey: clavesPublicas.configuracion,
    queryFn: async () => datos(await api.GET('/api/publico/configuracion')),
    staleTime: CINCO_MINUTOS,
  });
}

/** Encabezado, aviso y preguntas frecuentes de la portada; los edita el administrador. */
export function usePortada() {
  return useQuery({
    queryKey: clavesPublicas.portada,
    queryFn: async () => datos(await api.GET('/api/publico/portada')),
    staleTime: CINCO_MINUTOS,
  });
}

export function useTiposTramite() {
  return useQuery({
    queryKey: clavesPublicas.tiposTramite,
    queryFn: async () => datos(await api.GET('/api/publico/tipos-tramite')),
    staleTime: CINCO_MINUTOS,
  });
}

export function useMunicipios() {
  return useQuery({
    queryKey: clavesPublicas.municipios,
    queryFn: async () => datos(await api.GET('/api/publico/municipios')),
    staleTime: CINCO_MINUTOS,
  });
}

/** Todas las sedes con al menos un trámite que se puede agendar. */
export function useModulos() {
  return useQuery({
    queryKey: clavesPublicas.modulos,
    queryFn: async () => datos(await api.GET('/api/publico/modulos')),
    staleTime: CINCO_MINUTOS,
  });
}

export function useModulosDe(municipioId: number | null) {
  return useQuery({
    queryKey: clavesPublicas.modulosDe(municipioId ?? 0),
    queryFn: async () =>
      datos(
        await api.GET('/api/publico/municipios/{municipioId}/modulos', {
          params: { path: { municipioId: municipioId ?? 0 } },
        }),
      ),
    enabled: municipioId !== null,
    staleTime: CINCO_MINUTOS,
  });
}

export function useOfertas(moduloId: number | null) {
  return useQuery({
    queryKey: clavesPublicas.ofertas(moduloId ?? 0),
    queryFn: async () =>
      datos(
        await api.GET('/api/publico/modulos/{moduloId}/ofertas', {
          params: { path: { moduloId: moduloId ?? 0 } },
        }),
      ),
    enabled: moduloId !== null,
    staleTime: CINCO_MINUTOS,
  });
}

/** Disponibilidad de toda la ventana de la oferta (RF-02). */
export function useDisponibilidad(ofertaId: number | null) {
  return useQuery({
    queryKey: clavesPublicas.disponibilidad(ofertaId ?? 0),
    queryFn: async () =>
      datos(
        await api.GET('/api/publico/ofertas/{ofertaId}/disponibilidad', {
          params: { path: { ofertaId: ofertaId ?? 0 } },
        }),
      ),
    enabled: ofertaId !== null,
    staleTime: 30_000,
  });
}

/** Horas con lugar en una fecha; siempre se vuelven a pedir porque cambian con cada reserva. */
export function useCupos(ofertaId: number | null, fecha: string | null) {
  return useQuery({
    queryKey: clavesPublicas.cupos(ofertaId ?? 0, fecha ?? ''),
    queryFn: async () =>
      datos(
        await api.GET('/api/publico/ofertas/{ofertaId}/cupos', {
          params: { path: { ofertaId: ofertaId ?? 0 }, query: { fecha: fecha ?? '' } },
        }),
      ),
    enabled: ofertaId !== null && fecha !== null,
    staleTime: 0,
  });
}
