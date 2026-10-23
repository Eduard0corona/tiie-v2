import { z } from 'zod';

const diasVentana = z
  .string()
  .trim()
  .refine(
    (v) => /^\d+$/.test(v) && Number(v) >= 1 && Number(v) <= 120,
    'Escribe un número de días entre 1 y 120.',
  );

/** Alta de una oferta: un trámite en un módulo. La ventana vacía toma el valor por omisión de la API. */
export const esquemaNuevaOferta = z.object({
  moduloId: z.string().min(1, 'Elige el módulo.'),
  tipoTramiteId: z.string().min(1, 'Elige el trámite.'),
  diasVentana: z
    .string()
    .trim()
    .refine((v) => v === '' || diasVentana.safeParse(v).success, {
      message: 'Escribe un número de días entre 1 y 120 o déjalo vacío.',
    }),
  notasSede: z.string().trim().max(500, 'Escribe 500 caracteres o menos.'),
});
export type ValoresNuevaOferta = z.infer<typeof esquemaNuevaOferta>;

export const esquemaOferta = z.object({
  diasVentana,
  notasSede: z.string().trim().max(500, 'Escribe 500 caracteres o menos.'),
  activo: z.boolean(),
});
export type ValoresOferta = z.infer<typeof esquemaOferta>;
