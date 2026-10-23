import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, datos } from '@/api/cliente';
import { clavesPublicas } from '../comun/usePublico';

export const claveMiCita = ['mi-cita'] as const;

/** Cita de la cookie `tiie_cita`; siempre se vuelve a pedir (docs/08#estado-del-servidor). */
export function useMiCita(habilitada: boolean) {
  return useQuery({
    queryKey: claveMiCita,
    queryFn: async () => datos(await api.GET('/api/mi-cita')),
    enabled: habilitada,
    staleTime: 0,
    retry: false,
  });
}

export function useCancelarMiCita() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: async (motivo: string | null) =>
      datos(await api.POST('/api/mi-cita/cancelacion', { body: { motivo } })),
    onSuccess: (detalle) => {
      cliente.setQueryData(claveMiCita, detalle);
      void cliente.invalidateQueries({ queryKey: clavesPublicas.disponibilidad(detalle.ofertaId) });
    },
  });
}

/** RN-17: otro horario de la misma oferta; la cookie pasa a la cita nueva. */
export function useReprogramarMiCita() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: async (cupoId: number) =>
      datos(await api.POST('/api/mi-cita/reprogramacion', { body: { cupoId } })),
    onSuccess: (detalle) => {
      cliente.setQueryData(claveMiCita, detalle);
      void cliente.invalidateQueries({ queryKey: clavesPublicas.disponibilidad(detalle.ofertaId) });
      void cliente.invalidateQueries({ queryKey: ['publico', 'cupos', detalle.ofertaId] });
    },
  });
}

export function useSalirMiCita() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/mi-cita/salida');
    },
    onSettled: () => cliente.removeQueries({ queryKey: claveMiCita }),
  });
}
