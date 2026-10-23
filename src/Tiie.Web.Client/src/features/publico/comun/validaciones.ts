import { z } from 'zod';
import { interpretarCurp } from '@/lib/curp';

/** CURP con la misma regla que el backend (RN-15); solo da retroalimentación, la API valida de nuevo. */
export const campoCurp = z
  .string()
  .trim()
  .refine((v) => interpretarCurp(v) !== null, 'La CURP no es válida. Revisa las 18 letras y números.');

export const campoCorreo = z.string().trim().pipe(z.email('Escribe un correo válido.'));
