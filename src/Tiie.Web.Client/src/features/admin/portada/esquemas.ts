import { z } from 'zod';
import type { components } from '@/api/esquema';
import { enlacesPermitidos } from '@/lib/enlaces';

type PortadaAdmin = components['schemas']['PortadaAdmin'];
type DatosPortada = components['schemas']['DatosPortada'];

export const PREGUNTAS_MAXIMAS = 20;
const MENSAJE_ENLACES = 'Los enlaces deben empezar con /, https://, mailto: o tel:.';
const maximo = (n: number) => `Escribe ${n} caracteres o menos.`;

/** Formato del contenido de la portada; la API valida de nuevo y decide si el aviso está vigente. */
export const esquemaPortada = z
  .object({
    etiqueta: z.string().trim().min(1, 'Escribe la etiqueta.').max(80, maximo(80)),
    titulo: z.string().trim().min(1, 'Escribe el título.').max(120, maximo(120)),
    tituloDestacado: z.string().trim().max(120, maximo(120)),
    descripcion: z.string().trim().min(1, 'Escribe la descripción.').max(300, maximo(300)),
    avisoTexto: z.string().trim().max(300, maximo(300)).refine(enlacesPermitidos, MENSAJE_ENLACES),
    avisoTono: z.enum(['Informativo', 'Importante']),
    avisoDesde: z.string(),
    avisoHasta: z.string(),
    preguntas: z
      .array(
        z.object({
          pregunta: z.string().trim().min(1, 'Escribe la pregunta.').max(200, maximo(200)),
          respuesta: z
            .string()
            .trim()
            .min(1, 'Escribe la respuesta.')
            .max(1500, maximo(1500))
            .refine(enlacesPermitidos, MENSAJE_ENLACES),
        }),
      )
      .max(PREGUNTAS_MAXIMAS, `Agrega ${PREGUNTAS_MAXIMAS} preguntas o menos.`),
  })
  .superRefine((v, ctx) => {
    if (!v.avisoTexto && (v.avisoDesde || v.avisoHasta))
      ctx.addIssue({ code: 'custom', path: ['avisoTexto'], message: 'Escribe el aviso o borra sus fechas.' });
    if (v.avisoDesde && v.avisoHasta && v.avisoHasta < v.avisoDesde)
      ctx.addIssue({
        code: 'custom',
        path: ['avisoHasta'],
        message: 'La fecha final no puede ser anterior a la inicial.',
      });
  });
export type ValoresPortada = z.infer<typeof esquemaPortada>;

export function valoresDePortada(p: PortadaAdmin): ValoresPortada {
  return {
    etiqueta: p.etiqueta,
    titulo: p.titulo,
    tituloDestacado: p.tituloDestacado ?? '',
    descripcion: p.descripcion,
    avisoTexto: p.avisoTexto ?? '',
    avisoTono: p.avisoTono,
    avisoDesde: p.avisoDesde ?? '',
    avisoHasta: p.avisoHasta ?? '',
    preguntas: p.preguntas.map((q) => ({ pregunta: q.pregunta, respuesta: q.respuesta })),
  };
}

/** Todo el contenido en una sola petición: la API reemplaza las preguntas en este orden. */
export function datosDePortada(v: ValoresPortada, version: string | null): DatosPortada {
  return {
    etiqueta: v.etiqueta.trim(),
    titulo: v.titulo.trim(),
    tituloDestacado: v.tituloDestacado.trim() || null,
    descripcion: v.descripcion.trim(),
    avisoTexto: v.avisoTexto.trim() || null,
    avisoTono: v.avisoTono,
    avisoDesde: v.avisoDesde || null,
    avisoHasta: v.avisoHasta || null,
    preguntas: v.preguntas.map((q) => ({ pregunta: q.pregunta.trim(), respuesta: q.respuesta.trim() })),
    version,
  };
}
