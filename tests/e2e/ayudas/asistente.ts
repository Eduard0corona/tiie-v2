import { expect, type Locator, type Page } from '@playwright/test';
import type { Lugar } from './api';
import { etiquetaDia } from './fechas';
import type { Persona } from './persona';

function escapar(texto: string): string {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** Opción cuyo nombre accesible empieza con `texto` (las tarjetas llevan dirección o costo después). */
export function empiezaCon(texto: string): RegExp {
  return new RegExp(`^${escapar(texto)}`);
}

/**
 * Botón de un día del calendario; avanza de mes hasta mostrarlo. Su nombre accesible dice el estado:
 * «viernes 23 de octubre, alta disponibilidad».
 */
export async function diaDelCalendario(page: Page, fecha: string): Promise<Locator> {
  const dia = page.getByRole('button', { name: new RegExp(`^${escapar(etiquetaDia(fecha))},`) });
  await expect(page.getByRole('button', { name: 'Mes siguiente' })).toBeVisible();
  for (let mes = 0; mes < 4 && !(await dia.isVisible()); mes++)
    await page.getByRole('button', { name: 'Mes siguiente' }).click();
  return dia;
}

export async function elegirDia(page: Page, fecha: string): Promise<void> {
  await (await diaDelCalendario(page, fecha)).click();
}

/**
 * Elige una tarjeta del asistente. El radio es controlado por la URL: se marca cuando la URL cambia, no al
 * hacer clic, por eso se espera el estado en lugar de usar `check()`.
 */
export async function elegirOpcion(opcion: Locator): Promise<void> {
  await opcion.click();
  await expect(opcion).toBeChecked();
}

/** Elige una hora del día; el radio está oculto y se elige con su tarjeta. */
export async function elegirHora(page: Page, hora: string): Promise<void> {
  await page
    .getByRole('group', { name: /^Horas con lugar/ })
    .locator('label')
    .filter({ hasText: new RegExp(`^${escapar(hora)}`) })
    .click();
}

/** Abre el asistente en el paso de la hora del lugar (la selección vive en la URL) y llega a «Tus datos». */
export async function abrirPasoDatos(page: Page, lugar: Lugar): Promise<void> {
  const { oferta, fecha, cupo } = lugar;
  await page.goto(
    `/agendar?municipio=${oferta.municipioId}&modulo=${oferta.moduloId}&oferta=${oferta.ofertaId}&fecha=${fecha}&paso=hora`,
  );
  await expect(page.getByRole('heading', { name: 'Elige la hora' })).toBeVisible();
  await elegirHora(page, cupo.hora);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Tus datos' })).toBeVisible();
}

export async function llenarDatos(page: Page, persona: Persona): Promise<void> {
  await page.getByLabel('CURP').fill(persona.curp);
  await page.getByLabel('Nombre o nombres').fill(persona.nombre);
  await page.getByLabel('Primer apellido').fill(persona.apellidoPaterno);
  await page.getByLabel('Segundo apellido (opcional)').fill(persona.apellidoMaterno);
  await page.getByLabel('Celular').fill(persona.celular);
  await page.getByLabel('Correo electrónico').fill(persona.correo);
  await page.getByLabel('Leí y acepto el aviso de privacidad').check();
}

/** Espera el token de Turnstile; con las claves de prueba llega solo, sin desafío. */
export async function esperarCaptcha(page: Page): Promise<void> {
  await expect(page.locator('input[name="cf-turnstile-response"]')).toHaveValue(/.+/, { timeout: 30_000 });
}
