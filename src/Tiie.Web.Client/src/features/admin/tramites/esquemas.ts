import { z } from 'zod';
import type { components } from '@/api/esquema';

type DatosTipoTramite = components['schemas']['DatosTipoTramite'];
type TipoTramiteDetalle = components['schemas']['TipoTramiteDetalle'];

/** Formato de un tipo de trámite y sus requisitos; la API valida de nuevo. */
export const esquemaTramite = z.object({
  nombre: z
    .string()
    .trim()
    .min(1, 'Escribe el nombre del trámite.')
    .max(120, 'Escribe 120 caracteres o menos.'),
  descripcion: z.string().trim().max(500, 'Escribe 500 caracteres o menos.'),
  costo: z
    .string()
    .trim()
    .refine(
      (v) => v === '' || /^\d+(\.\d{1,2})?$/.test(v),
      'Escribe el costo en pesos, por ejemplo 150 o 150.50.',
    ),
  notas: z.string().trim().max(500, 'Escribe 500 caracteres o menos.'),
  activo: z.boolean(),
  requisitos: z.array(
    z.object({
      nivelEscolarId: z.string(),
      texto: z.string().trim().min(1, 'Escribe el requisito.').max(300, 'Escribe 300 caracteres o menos.'),
    }),
  ),
});
export type ValoresTramite = z.infer<typeof esquemaTramite>;

export const TRAMITE_VACIO: ValoresTramite = {
  nombre: '',
  descripcion: '',
  costo: '',
  notas: '',
  activo: true,
  requisitos: [],
};

export function valoresDeTramite(t: TipoTramiteDetalle): ValoresTramite {
  return {
    nombre: t.nombre,
    descripcion: t.descripcion ?? '',
    costo: t.costo === null ? '' : String(t.costo),
    notas: t.notas ?? '',
    activo: t.activo,
    requisitos: [...t.requisitos]
      .sort((a, b) => a.orden - b.orden)
      .map((r) => ({
        nivelEscolarId: r.nivelEscolarId === null ? '' : String(r.nivelEscolarId),
        texto: r.texto,
      })),
  };
}

/** La lista de requisitos se envía completa y en orden: la API la reemplaza. */
export function datosDeTramite(v: ValoresTramite, version: string | null): DatosTipoTramite {
  return {
    nombre: v.nombre.trim(),
    descripcion: v.descripcion.trim() || null,
    costo: v.costo.trim() === '' ? null : Number(v.costo),
    notas: v.notas.trim() || null,
    activo: v.activo,
    requisitos: v.requisitos.map((r) => ({
      nivelEscolarId: r.nivelEscolarId ? Number(r.nivelEscolarId) : null,
      texto: r.texto.trim(),
    })),
    version,
  };
}
