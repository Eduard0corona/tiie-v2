/**
 * Texto con enlaces `[texto](destino)` del contenido que edita el administrador. La misma expresión y los
 * mismos destinos permitidos están en `Tiie.Dominio/Sitio/ReglasPortada.cs`; la API rechaza los demás y aquí,
 * por si acaso, se muestran como texto.
 */
export type Tramo =
  { tipo: 'texto'; texto: string } | { tipo: 'enlace'; texto: string; destino: string; interno: boolean };

const ENLACE = /\[([^[\]\n]+)\]\(([^()\s]+)\)/g;

export function destinoPermitido(destino: string): boolean {
  return (
    (destino.startsWith('/') && !destino.startsWith('//') && !destino.includes('\\')) ||
    /^(https:\/\/|mailto:|tel:)/i.test(destino)
  );
}

export function tramosDeTexto(texto: string): Tramo[] {
  const tramos: Tramo[] = [];
  let desde = 0;
  for (const coincidencia of texto.matchAll(ENLACE)) {
    const [completo, etiqueta, destino] = coincidencia as unknown as [string, string, string];
    const inicio = coincidencia.index;
    if (!destinoPermitido(destino)) continue;
    if (inicio > desde) tramos.push({ tipo: 'texto', texto: texto.slice(desde, inicio) });
    tramos.push({ tipo: 'enlace', texto: etiqueta, destino, interno: destino.startsWith('/') });
    desde = inicio + completo.length;
  }
  if (desde < texto.length) tramos.push({ tipo: 'texto', texto: texto.slice(desde) });
  return tramos;
}

/** Todos los enlaces del texto van a destinos permitidos; los demás la API los rechaza. */
export function enlacesPermitidos(texto: string): boolean {
  return [...texto.matchAll(ENLACE)].every((m) => destinoPermitido(m[2] ?? ''));
}
