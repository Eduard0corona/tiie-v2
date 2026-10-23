import { createHmac } from 'node:crypto';

const ALFABETO = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
const PASO_MS = 30_000;

/** RFC 6238 con SHA-1, 30 segundos y 6 dígitos, como las apps autenticadoras y ASP.NET Core Identity. */
export function totp(secretoBase32: string, instante = Date.now()): string {
  let bits = '';
  for (const c of secretoBase32.replace(/[\s=-]/g, '').toUpperCase())
    bits += ALFABETO.indexOf(c).toString(2).padStart(5, '0');
  const llave = Buffer.from((bits.match(/.{8}/g) ?? []).map((b) => parseInt(b, 2)));
  const contador = Buffer.alloc(8);
  contador.writeBigUInt64BE(BigInt(Math.floor(instante / PASO_MS)));
  const hmac = createHmac('sha1', llave).update(contador).digest();
  const desplazamiento = (hmac[hmac.length - 1] ?? 0) & 0xf;
  const valor = (hmac.readUInt32BE(desplazamiento) & 0x7fffffff) % 1_000_000;
  return valor.toString().padStart(6, '0');
}

/**
 * Código con al menos 5 segundos de vigencia, para que no venza entre que se escribe y se envía.
 * El reloj del equipo sí importa aquí: el código depende de la hora, igual que en el celular.
 */
export async function codigoVigente(secretoBase32: string): Promise<string> {
  const restante = PASO_MS - (Date.now() % PASO_MS);
  if (restante < 5_000) await new Promise((r) => setTimeout(r, restante + 250));
  return totp(secretoBase32);
}
