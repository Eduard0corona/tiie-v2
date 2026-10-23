import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

// Buzón de prueba: la carpeta donde el proveedor de correo `Archivo` deja cada mensaje como .eml
// (src/Tiie.Infraestructura/Correo/CorreoArchivo.cs). En QA el correo sale por Communication Services y no
// hay carpeta: las pruebas que leen correos se omiten.

const PREDETERMINADA = resolve(import.meta.dirname, '..', '..', '..', 'artifacts', 'correos');

export const MOTIVO_SIN_BUZON =
  'Sin buzón de prueba: define E2E_CARPETA_CORREOS con la carpeta del proveedor de correo Archivo ' +
  '(en local, artifacts/correos). En QA el correo sale por Communication Services.';

export function carpetaCorreos(): string | null {
  const carpeta = process.env.E2E_CARPETA_CORREOS
    ? resolve(process.env.E2E_CARPETA_CORREOS)
    : PREDETERMINADA;
  return existsSync(carpeta) && statSync(carpeta).isDirectory() ? carpeta : null;
}

export interface Correo {
  para: string;
  asunto: string;
  texto: string;
}

function decodificarEncabezado(valor: string): string {
  return valor.replace(/=\?utf-8\?B\?([^?]*)\?=/gi, (_, b64: string) =>
    Buffer.from(b64, 'base64').toString('utf8'),
  );
}

function leer(ruta: string): Correo {
  const crudo = readFileSync(ruta, 'utf8');
  const [encabezados = ''] = crudo.split('\r\n\r\n', 1);
  const encabezado = (nombre: string) =>
    new RegExp(`^${nombre}: (.*)$`, 'mi').exec(encabezados)?.[1]?.trim() ?? '';
  // Parte text/plain en base64; el HTML lleva los mismos enlaces.
  const parte = /Content-Type: text\/plain[^\r\n]*\r\nContent-Transfer-Encoding: base64\r\n\r\n([\s\S]*?)\r\n\r\n/i.exec(
    crudo,
  );
  return {
    para: encabezado('To'),
    asunto: decodificarEncabezado(encabezado('Subject')),
    texto: parte?.[1] ? Buffer.from(parte[1].replace(/\s/g, ''), 'base64').toString('utf8') : '',
  };
}

/**
 * Espera el primer correo para `destinatario` que cumpla `filtro`. Los correos al ciudadano salen por la
 * bandeja de salida, que se revisa cada 15 segundos (docs/10).
 */
export async function esperarCorreo(
  destinatario: string,
  filtro: (correo: Correo) => boolean = () => true,
  tiempoMaximo = 90_000,
): Promise<Correo> {
  const carpeta = carpetaCorreos();
  if (!carpeta) throw new Error(MOTIVO_SIN_BUZON);
  const limite = Date.now() + tiempoMaximo;
  while (Date.now() < limite) {
    for (const archivo of readdirSync(carpeta).filter((a) => a.endsWith('.eml'))) {
      const ruta = join(carpeta, archivo);
      // Solo se leen los correos que pueden ser de esta prueba: el destinatario es único por prueba.
      if (!readFileSync(ruta, 'utf8').includes(`To: ${destinatario}\r\n`)) continue;
      const correo = leer(ruta);
      if (filtro(correo)) return correo;
    }
    await new Promise((r) => setTimeout(r, 1_000));
  }
  throw new Error(`No llegó el correo esperado para ${destinatario} en ${tiempoMaximo / 1000} s.`);
}

/** Ruta y consulta del primer enlace del correo que coincide con `patron`, para abrirla en la URL base. */
export function enlace(correo: Correo, patron: RegExp): string {
  const encontrado = correo.texto.match(/https?:\/\/[^\s<>"]+/g)?.find((u) => patron.test(u));
  if (!encontrado) throw new Error(`El correo «${correo.asunto}» no trae un enlace que coincida con ${patron}.`);
  const url = new URL(encontrado);
  return url.pathname + url.search;
}
