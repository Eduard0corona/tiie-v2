import { z } from 'zod';
import type { components } from '@/api/esquema';

type UsuarioDetalle = components['schemas']['UsuarioDetalle'];

const texto = (mensaje: string, maximo = 80) =>
  z.string().trim().min(1, mensaje).max(maximo, `Escribe ${maximo} caracteres o menos.`);

/** Formato de la cuenta del personal; la API valida de nuevo (usuario único, último administrador). */
export const esquemaUsuario = z.object({
  usuario: z
    .string()
    .trim()
    .regex(/^[A-Za-z0-9._@-]{3,50}$/, 'Usa de 3 a 50 letras sin acentos, números o . _ - @'),
  correo: z.string().trim().pipe(z.email('Escribe un correo válido.')),
  nombre: texto('Escribe el nombre.'),
  apellidoPaterno: texto('Escribe el primer apellido.'),
  apellidoMaterno: z.string().trim().max(80, 'Escribe 80 caracteres o menos.'),
  rol: z.string().min(1, 'Elige el rol.'),
  moduloIds: z.array(z.string()),
});
export type ValoresUsuario = z.infer<typeof esquemaUsuario>;

export const CAMPOS_USUARIO = Object.keys(esquemaUsuario.shape) as (keyof ValoresUsuario)[];

export const USUARIO_VACIO: ValoresUsuario = {
  usuario: '',
  correo: '',
  nombre: '',
  apellidoPaterno: '',
  apellidoMaterno: '',
  rol: '',
  moduloIds: [],
};

export function valoresDeUsuario(u: UsuarioDetalle): ValoresUsuario {
  return {
    usuario: u.usuario,
    correo: u.correo,
    nombre: u.nombre,
    apellidoPaterno: u.apellidoPaterno,
    apellidoMaterno: u.apellidoMaterno ?? '',
    rol: u.rol ?? '',
    moduloIds: u.moduloIds.map(String),
  };
}
