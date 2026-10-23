import { z } from 'zod';
import type { components } from '@/api/esquema';
import { esFechaIso } from '@/lib/fechas';

type CrearBloqueo = components['schemas']['CrearBloqueo'];

const fecha = z.string().refine((v) => esFechaIso(v), 'Elige una fecha.');

/** Un bloqueo general cierra todos los módulos; uno de módulo o de oferta pide a cuál se aplica. */
export const esquemaBloqueo = z
  .object({
    alcance: z.enum(['General', 'Modulo', 'Oferta']),
    desde: fecha,
    hasta: fecha,
    moduloId: z.string(),
    ofertaId: z.string(),
    motivo: z.string().trim().min(1, 'Escribe el motivo.').max(200, 'Escribe 200 caracteres o menos.'),
  })
  .superRefine((v, ctx) => {
    if (esFechaIso(v.desde) && esFechaIso(v.hasta) && v.hasta < v.desde) {
      ctx.addIssue({ code: 'custom', path: ['hasta'], message: 'Debe ser igual o posterior a «Desde».' });
    }
    if (v.alcance !== 'General' && !v.moduloId) {
      ctx.addIssue({ code: 'custom', path: ['moduloId'], message: 'Elige el módulo.' });
    }
    if (v.alcance === 'Oferta' && !v.ofertaId) {
      ctx.addIssue({ code: 'custom', path: ['ofertaId'], message: 'Elige el trámite.' });
    }
  });
export type ValoresBloqueo = z.infer<typeof esquemaBloqueo>;

/** La API pide el módulo o la oferta, no ambos: la oferta ya indica su módulo. */
export function datosDeBloqueo(v: ValoresBloqueo): CrearBloqueo {
  return {
    desde: v.desde,
    hasta: v.hasta,
    moduloId: v.alcance === 'Modulo' ? Number(v.moduloId) : null,
    ofertaId: v.alcance === 'Oferta' ? Number(v.ofertaId) : null,
    motivo: v.motivo.trim(),
  };
}
