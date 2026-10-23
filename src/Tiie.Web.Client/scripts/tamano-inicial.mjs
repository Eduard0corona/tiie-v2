// Mide el JavaScript inicial del sitio público ya compilado: el script de entrada de index.html y los
// módulos que precarga, comprimidos con gzip. Falla si supera el presupuesto de RNF-14 (docs/08#rendimiento).
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';

const PRESUPUESTO_KB = 200;
const raiz = join(import.meta.dirname, '..', '..', 'Tiie.Web', 'wwwroot');
const html = readFileSync(join(raiz, 'index.html'), 'utf8');

const rutas = [
  ...html.matchAll(/<script[^>]+type="module"[^>]+src="([^"]+)"/g),
  ...html.matchAll(/<link[^>]+rel="modulepreload"[^>]+href="([^"]+)"/g),
].map((m) => m[1]);

let total = 0;
for (const ruta of rutas) {
  const bytes = gzipSync(readFileSync(join(raiz, ruta))).length;
  total += bytes;
  console.log(`${(bytes / 1024).toFixed(1).padStart(7)} KB  ${ruta}`);
}
const totalKb = total / 1024;
console.log(`${totalKb.toFixed(1).padStart(7)} KB  total comprimido (presupuesto ${PRESUPUESTO_KB} KB)`);

if (rutas.length === 0 || totalKb > PRESUPUESTO_KB) {
  console.error(
    rutas.length === 0
      ? 'No se encontró el script de entrada.'
      : 'El JavaScript inicial supera el presupuesto.',
  );
  process.exit(1);
}
