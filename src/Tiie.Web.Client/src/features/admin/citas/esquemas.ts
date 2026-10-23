import { z } from 'zod';
import { campoCurp } from '@/features/publico/comun/validaciones';

const texto80 = (mensaje: string) =>
  z.string().trim().min(1, mensaje).max(80, 'Escribe 80 caracteres o menos.');

/** Datos de la persona en ventanilla; celular y correo son opcionales (RF-08). Solo formato: la API valida de nuevo. */
export const esquemaVentanilla = z.object({
  curp: campoCurp,
  nombre: texto80('Escribe el nombre.'),
  apellidoPaterno: texto80('Escribe el primer apellido.'),
  apellidoMaterno: z.string().trim().max(80, 'Escribe 80 caracteres o menos.'),
  celular: z
    .string()
    .trim()
    .refine((v) => v === '' || /^\d{10}$/.test(v), 'Escribe los 10 dígitos del celular o déjalo vacío.'),
  correo: z
    .string()
    .trim()
    .refine((v) => v === '' || z.email().safeParse(v).success, 'Escribe un correo válido o déjalo vacío.'),
  avisoLeido: z.boolean().refine((v) => v, 'Confirma que leíste el aviso de privacidad a la persona.'),
});
export type ValoresVentanilla = z.infer<typeof esquemaVentanilla>;

export const CAMPOS_VENTANILLA = [
  'curp',
  'nombre',
  'apellidoPaterno',
  'apellidoMaterno',
  'celular',
  'correo',
  'avisoLeido',
] as const satisfies readonly (keyof ValoresVentanilla)[];
