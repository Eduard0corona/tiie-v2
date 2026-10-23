import { useQuery } from '@tanstack/react-query';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { horaLocal, hoyLocal } from '@/lib/fechas';

type CupoAdmin = components['schemas']['CupoAdmin'];
type CupoDisponible = components['schemas']['CupoDisponible'];

/**
 * Horas con lugar para registrar o reprogramar: abiertas, con espacio y, si son de hoy, por venir. Es solo
 * para elegir; la API decide al reservar (RN-02, RN-07).
 */
export function cuposConLugar(cupos: readonly CupoAdmin[], hoy: string, ahora: string): CupoDisponible[] {
  return cupos
    .filter(
      (c) => !c.cerrado && c.ocupados < c.capacidad && (c.fecha > hoy || (c.fecha === hoy && c.hora > ahora)),
    )
    .map((c) => ({ cupoId: c.cupoId, hora: c.hora, libres: c.capacidad - c.ocupados }));
}

/** Cupos de una oferta en un día; el personal también ve los de hoy. */
export function useCuposDelDia(ofertaId: number | null, fecha: string | null) {
  return useQuery({
    queryKey: clavesAdmin.cupos(ofertaId ?? 0, fecha ?? '', fecha ?? ''),
    queryFn: async () =>
      datos(
        await api.GET('/api/admin/ofertas/{ofertaId}/cupos', {
          params: { path: { ofertaId: ofertaId ?? 0 }, query: { desde: fecha ?? '', hasta: fecha ?? '' } },
        }),
      ),
    enabled: ofertaId !== null && fecha !== null,
    staleTime: 0,
    select: (cupos) => cuposConLugar(cupos, hoyLocal(), horaLocal()),
  });
}
