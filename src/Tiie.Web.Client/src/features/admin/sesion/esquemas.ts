import { z } from 'zod';

/** Solo formato, para dar retroalimentación; la API valida de nuevo. */

export const esquemaEntrar = z.object({
  usuario: z.string().trim().min(1, 'Escribe tu usuario.'),
  contrasena: z.string().min(1, 'Escribe tu contraseña.'),
});
export type ValoresEntrar = z.infer<typeof esquemaEntrar>;

export const esquemaCodigoMfa = z.object({
  codigo: z
    .string()
    .trim()
    .regex(/^\d{3}[\s-]?\d{3}$/, 'Escribe los 6 dígitos de tu app autenticadora.'),
  recordarEsteEquipo: z.boolean(),
});
export type ValoresCodigoMfa = z.infer<typeof esquemaCodigoMfa>;

export const esquemaRecuperacion = z.object({
  codigoRecuperacion: z.string().trim().min(1, 'Escribe uno de tus códigos de recuperación.'),
});
export type ValoresRecuperacion = z.infer<typeof esquemaRecuperacion>;

export const esquemaOlvide = z.object({
  correo: z.string().trim().pipe(z.email('Escribe un correo válido.')),
});
export type ValoresOlvide = z.infer<typeof esquemaOlvide>;

export const esquemaActivarMfa = z.object({
  codigo: z
    .string()
    .trim()
    .regex(/^\d{3}[\s-]?\d{3}$/, 'Escribe los 6 dígitos que muestra tu app autenticadora.'),
});
export type ValoresActivarMfa = z.infer<typeof esquemaActivarMfa>;

/** 12 caracteres o más, sin reglas de composición (ADR 0003). */
const contrasenaNueva = z.string().min(12, 'Usa al menos 12 caracteres.');

export const esquemaContrasenaNueva = z
  .object({
    contrasena: contrasenaNueva,
    confirmacion: z.string(),
  })
  .refine((v) => v.contrasena === v.confirmacion, {
    error: 'Las contraseñas no coinciden.',
    path: ['confirmacion'],
  });
export type ValoresContrasenaNueva = z.infer<typeof esquemaContrasenaNueva>;

export const esquemaCambioContrasena = z
  .object({
    actual: z.string().min(1, 'Escribe tu contraseña actual.'),
    nueva: contrasenaNueva,
    confirmacion: z.string(),
  })
  .refine((v) => v.nueva === v.confirmacion, {
    error: 'Las contraseñas no coinciden.',
    path: ['confirmacion'],
  });
export type ValoresCambioContrasena = z.infer<typeof esquemaCambioContrasena>;
