import { z } from 'zod';
import { campoCorreo, campoCurp } from '../comun/validaciones';

/** Solo formato, para dar retroalimentación; la API valida de nuevo (RN-15). */
export const esquemaDatos = z.object({
  curp: campoCurp,
  nombre: z.string().trim().min(1, 'Escribe el nombre.').max(80, 'Escribe 80 caracteres o menos.'),
  apellidoPaterno: z
    .string()
    .trim()
    .min(1, 'Escribe el primer apellido.')
    .max(80, 'Escribe 80 caracteres o menos.'),
  apellidoMaterno: z.string().trim().max(80, 'Escribe 80 caracteres o menos.'),
  celular: z
    .string()
    .trim()
    .regex(/^\d{10}$/, 'Escribe los 10 dígitos de tu celular.'),
  correo: campoCorreo,
  aceptaAvisoPrivacidad: z.boolean().refine((v) => v, 'Acepta el aviso de privacidad para continuar.'),
});
export type ValoresDatos = z.infer<typeof esquemaDatos>;

export const CAMPOS_DATOS = [
  'curp',
  'nombre',
  'apellidoPaterno',
  'apellidoMaterno',
  'celular',
  'correo',
  'aceptaAvisoPrivacidad',
] as const satisfies readonly (keyof ValoresDatos)[];
