import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';

/**
 * axe en la pantalla actual (docs/12#extremo-a-extremo): falla con hallazgos graves o críticos de WCAG 2.1
 * A y AA. El widget de Turnstile es de Cloudflare y vive en su propio iframe: no se revisa. En celular,
 * además, la página no debe desplazarse a lo ancho (WCAG 1.4.10, docs/08).
 */
export async function revisarAccesibilidad(page: Page, pantalla: string): Promise<void> {
  const resultado = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .exclude('iframe[src*="challenges.cloudflare.com"]')
    .analyze();
  const graves = resultado.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  const detalle = graves
    .map((v) => `- ${v.id} (${v.impact}): ${v.help}\n  ${v.nodes.map((n) => n.target.join(' ')).join('\n  ')}`)
    .join('\n');
  expect(graves, `Hallazgos graves de accesibilidad en «${pantalla}»:\n${detalle}`).toEqual([]);
  await sinDesbordeHorizontal(page, pantalla);
}

/** En pantallas angostas, el documento no es más ancho que la ventana; dice qué elemento lo ensancha. */
export async function sinDesbordeHorizontal(page: Page, pantalla: string): Promise<void> {
  const medida = await page.evaluate(() => {
    const ancho = document.documentElement.clientWidth;
    const culpable = [...document.querySelectorAll('body *')].find(
      (e) => e.getBoundingClientRect().right > ancho + 1 && getComputedStyle(e).position === 'absolute',
    );
    const ruta = (e: Element | undefined) => {
      const partes: string[] = [];
      for (let x = e ?? null; x && partes.length < 4; x = x.parentElement)
        partes.push(`${x.tagName.toLowerCase()}${x.classList.length ? '.' + [...x.classList].slice(0, 2).join('.') : ''}`);
      return partes.join(' < ');
    };
    return { ancho, documento: document.documentElement.scrollWidth, culpable: ruta(culpable) };
  });
  if (medida.ancho >= 768) return;
  expect(
    medida.documento,
    `«${pantalla}» se desplaza a lo ancho: el documento mide ${medida.documento} px en una ventana de ` +
      `${medida.ancho} px${medida.culpable ? ` (elemento posicionado fuera: ${medida.culpable})` : ''}.`,
  ).toBeLessThanOrEqual(medida.ancho);
}
