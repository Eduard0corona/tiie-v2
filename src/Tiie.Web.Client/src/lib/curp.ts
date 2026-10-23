/**
 * Validación de la CURP (RN-15). Es la misma regla que src/Tiie.Dominio/Solicitantes/Curp.cs; ambas se
 * prueban con tests/vectores-curp.json. Solo da retroalimentación: la API valida de nuevo.
 */

export type Sexo = 'H' | 'M' | 'X';

export interface DatosCurp {
  curp: string;
  /** 'yyyy-MM-dd' */
  fechaNacimiento: string;
  sexo: Sexo;
}

export const LONGITUD_CURP = 18;

const DICCIONARIO = '0123456789ABCDEFGHIJKLMNÑOPQRSTUVWXYZ';

const FORMATO = new RegExp(
  '^[A-Z][AEIOUX][A-Z]{2}[0-9]{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[HMX]' +
    '(AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)' +
    '[B-DF-HJ-NP-TV-Z]{3}[A-Z0-9][0-9]$',
);

/** Mayúsculas y sin espacios. */
export function normalizarCurp(curp: string): string {
  return curp.replace(/\s/g, '').toUpperCase();
}

/** Dígito verificador de la posición 18 a partir de las primeras 17 posiciones. */
export function calcularDigitoVerificador(primeras17: string): number {
  if (primeras17.length !== LONGITUD_CURP - 1) throw new Error('Se esperan 17 caracteres.');
  let suma = 0;
  for (let i = 0; i < primeras17.length; i++) {
    suma += DICCIONARIO.indexOf(primeras17.charAt(i)) * (18 - i);
  }
  return (10 - (suma % 10)) % 10;
}

function esBisiesto(anio: number): boolean {
  return (anio % 4 === 0 && anio % 100 !== 0) || anio % 400 === 0;
}

function diasDelMes(anio: number, mes: number): number {
  if (mes === 2) return esBisiesto(anio) ? 29 : 28;
  return [4, 6, 9, 11].includes(mes) ? 30 : 31;
}

/** Normaliza y valida; devuelve fecha de nacimiento y sexo, o null si no es válida. */
export function interpretarCurp(curp: string | null | undefined): DatosCurp | null {
  if (curp == null) return null;
  const normalizada = normalizarCurp(curp);
  if (!FORMATO.test(normalizada)) return null;

  // El siglo lo da la posición 17: dígito = 1900 a 1999, letra = 2000 en adelante.
  const siglo = /[0-9]/.test(normalizada.charAt(16)) ? 1900 : 2000;
  const anio = siglo + Number(normalizada.slice(4, 6));
  const mes = Number(normalizada.slice(6, 8));
  const dia = Number(normalizada.slice(8, 10));
  if (dia > diasDelMes(anio, mes)) return null;

  if (Number(normalizada.charAt(17)) !== calcularDigitoVerificador(normalizada.slice(0, 17))) return null;

  const dosDigitos = (n: number) => String(n).padStart(2, '0');
  return {
    curp: normalizada,
    fechaNacimiento: `${anio}-${dosDigitos(mes)}-${dosDigitos(dia)}`,
    sexo: normalizada.charAt(10) as Sexo,
  };
}

export function esCurpValida(curp: string | null | undefined): boolean {
  return interpretarCurp(curp) !== null;
}
