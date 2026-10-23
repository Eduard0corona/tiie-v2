import { z } from 'zod';
import type { components } from '@/api/esquema';

type DatosModulo = components['schemas']['DatosModulo'];
type ModuloDetalle = components['schemas']['ModuloDetalle'];

const requerido = (mensaje: string, maximo = 120) =>
  z.string().trim().min(1, mensaje).max(maximo, `Escribe ${maximo} caracteres o menos.`);
const opcional = (maximo = 120) => z.string().trim().max(maximo, `Escribe ${maximo} caracteres o menos.`);
const coordenada = (limite: number, mensaje: string) =>
  z
    .string()
    .trim()
    .refine((v) => v === '' || (Number.isFinite(Number(v)) && Math.abs(Number(v)) <= limite), mensaje);

/** Formato de los datos de un módulo; la API valida de nuevo. */
export const esquemaModulo = z.object({
  municipioId: z.string().min(1, 'Elige el municipio.'),
  nombre: requerido('Escribe el nombre del módulo.'),
  calle: requerido('Escribe la calle.', 100),
  numeroExterior: opcional(15),
  numeroInterior: opcional(15),
  colonia: requerido('Escribe la colonia.', 80),
  codigoPostal: z
    .string()
    .trim()
    .regex(/^\d{5}$/, 'Escribe los 5 dígitos del código postal.'),
  localidad: opcional(80),
  telefono: z
    .string()
    .trim()
    .refine((v) => v === '' || /^\d{10}$/.test(v), 'Escribe los 10 dígitos del teléfono o déjalo vacío.'),
  correo: z
    .string()
    .trim()
    .refine((v) => v === '' || z.email().safeParse(v).success, 'Escribe un correo válido o déjalo vacío.'),
  horarioAtencion: opcional(255),
  latitud: coordenada(90, 'Escribe una latitud entre -90 y 90.'),
  longitud: coordenada(180, 'Escribe una longitud entre -180 y 180.'),
});
export type ValoresModulo = z.infer<typeof esquemaModulo>;

export const CAMPOS_MODULO = Object.keys(esquemaModulo.shape) as (keyof ValoresModulo)[];

export const MODULO_VACIO: ValoresModulo = {
  municipioId: '',
  nombre: '',
  calle: '',
  numeroExterior: '',
  numeroInterior: '',
  colonia: '',
  codigoPostal: '',
  localidad: '',
  telefono: '',
  correo: '',
  horarioAtencion: '',
  latitud: '',
  longitud: '',
};

export function valoresDeModulo(m: ModuloDetalle): ValoresModulo {
  return {
    municipioId: String(m.municipioId),
    nombre: m.nombre,
    calle: m.calle,
    numeroExterior: m.numeroExterior,
    numeroInterior: m.numeroInterior ?? '',
    colonia: m.colonia,
    codigoPostal: m.codigoPostal,
    localidad: m.localidad ?? '',
    telefono: m.telefono ?? '',
    correo: m.correo ?? '',
    horarioAtencion: m.horarioAtencion ?? '',
    latitud: m.latitud === null ? '' : String(m.latitud),
    longitud: m.longitud === null ? '' : String(m.longitud),
  };
}

const textoONulo = (v: string) => v.trim() || null;
const numeroONulo = (v: string) => (v.trim() === '' ? null : Number(v));

/** El contrato pide cada campo opcional como `null` explícito. */
export function datosDeModulo(v: ValoresModulo, version: string | null): DatosModulo {
  return {
    municipioId: Number(v.municipioId),
    nombre: v.nombre.trim(),
    calle: v.calle.trim(),
    numeroExterior: textoONulo(v.numeroExterior),
    numeroInterior: textoONulo(v.numeroInterior),
    colonia: v.colonia.trim(),
    codigoPostal: v.codigoPostal.trim(),
    localidad: textoONulo(v.localidad),
    telefono: textoONulo(v.telefono),
    correo: textoONulo(v.correo),
    horarioAtencion: textoONulo(v.horarioAtencion),
    latitud: numeroONulo(v.latitud),
    longitud: numeroONulo(v.longitud),
    version,
  };
}
