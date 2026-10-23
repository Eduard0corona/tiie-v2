import { randomInt, randomUUID } from 'node:crypto';

// Datos de prueba que no dependen de citas existentes: cada persona tiene una CURP válida y nueva (RN-15).

const DICCIONARIO = '0123456789ABCDEFGHIJKLMNÑOPQRSTUVWXYZ';
const LETRAS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const VOCALES = 'AEIOU';
const CONSONANTES = 'BCDFGHJKLMNPQRSTVWXYZ';

function una(de: string): string {
  return de.charAt(randomInt(de.length));
}

/** Dígito verificador de la posición 18; misma regla que src/Tiie.Dominio/Solicitantes/Curp.cs. */
function digitoVerificador(primeras17: string): number {
  let suma = 0;
  for (let i = 0; i < 17; i++) suma += DICCIONARIO.indexOf(primeras17.charAt(i)) * (18 - i);
  return (10 - (suma % 10)) % 10;
}

/** CURP válida de alguien nacido en Sinaloa entre 2005 y 2015 (letra en la posición 17: siglo XXI). */
export function curpNueva(): string {
  const anio = 5 + randomInt(11);
  const mes = 1 + randomInt(12);
  const dia = 1 + randomInt(28);
  const base =
    una(LETRAS) +
    una(VOCALES) +
    una(LETRAS) +
    una(LETRAS) +
    [anio, mes, dia].map((n) => String(n).padStart(2, '0')).join('') +
    una('HM') +
    'SL' +
    una(CONSONANTES) +
    una(CONSONANTES) +
    una(CONSONANTES) +
    una(LETRAS);
  return base + digitoVerificador(base);
}

export interface Persona {
  curp: string;
  nombre: string;
  apellidoPaterno: string;
  apellidoMaterno: string;
  celular: string;
  correo: string;
}

export function personaNueva(): Persona {
  const id = randomUUID().slice(0, 8);
  return {
    curp: curpNueva(),
    nombre: 'Prueba',
    apellidoPaterno: 'Extremo',
    apellidoMaterno: 'Aextremo',
    celular: `66${String(randomInt(100_000_000)).padStart(8, '0')}`,
    correo: `e2e.${id}@example.com`,
  };
}
