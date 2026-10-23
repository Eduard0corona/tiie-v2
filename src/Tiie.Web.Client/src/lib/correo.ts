/**
 * Sugerencia para dominios de correo mal escritos («gmial.com» → «gmail.com»). El correo es el único canal para
 * recibir el enlace de la cita: un error de dedo deja a la persona sin acceso. Solo sugiere; nunca corrige solo.
 */
const DOMINIOS_COMUNES = [
  'gmail.com',
  'hotmail.com',
  'outlook.com',
  'yahoo.com',
  'yahoo.com.mx',
  'live.com',
  'live.com.mx',
  'icloud.com',
  'hotmail.es',
  'outlook.es',
  'prodigy.net.mx',
  'uas.edu.mx',
] as const;

/** Distancia de edición (inserción, borrado, sustitución y transposición de dos letras vecinas). */
function distancia(a: string, b: string): number {
  const ancho = b.length + 1;
  const d = new Array<number>((a.length + 1) * ancho).fill(0);
  const celda = (i: number, j: number) => d[i * ancho + j] ?? 0;
  for (let i = 0; i <= a.length; i++) d[i * ancho] = i;
  for (let j = 0; j <= b.length; j++) d[j] = j;
  for (let i = 1; i <= a.length; i++) {
    for (let j = 1; j <= b.length; j++) {
      const costo = a[i - 1] === b[j - 1] ? 0 : 1;
      let valor = Math.min(celda(i - 1, j) + 1, celda(i, j - 1) + 1, celda(i - 1, j - 1) + costo);
      if (i > 1 && j > 1 && a[i - 1] === b[j - 2] && a[i - 2] === b[j - 1])
        valor = Math.min(valor, celda(i - 2, j - 2) + 1);
      d[i * ancho + j] = valor;
    }
  }
  return celda(a.length, b.length);
}

/** Devuelve el correo con el dominio común más parecido, o null si el dominio ya es común o no se parece a ninguno. */
export function sugerirCorreo(correo: string): string | null {
  const limpio = correo.trim().toLowerCase();
  const arroba = limpio.lastIndexOf('@');
  if (arroba < 1 || arroba === limpio.length - 1) return null;
  const usuario = limpio.slice(0, arroba);
  const dominio = limpio.slice(arroba + 1);
  if ((DOMINIOS_COMUNES as readonly string[]).includes(dominio)) return null;

  // En dominios cortos, dos cambios ya dan otro dominio real (gmx.com no es gmail.com).
  const tolerancia = dominio.length < 8 ? 1 : 2;
  let mejor: { dominio: string; distancia: number } | null = null;
  for (const comun of DOMINIOS_COMUNES) {
    const d = distancia(dominio, comun);
    if (d <= tolerancia && (mejor === null || d < mejor.distancia)) mejor = { dominio: comun, distancia: d };
  }
  return mejor ? `${usuario}@${mejor.dominio}` : null;
}
