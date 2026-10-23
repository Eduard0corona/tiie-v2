import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { expect, request, type APIRequestContext, type Page, type TestInfo } from '@playwright/test';
import { codigoVigente } from './totp';

// Usuario del panel para las pruebas del personal: un Administrador con MFA (QA) o el administrador semilla
// (local). En local, si aún no tiene MFA, la preparación lo configura y guarda la clave en .auth/.

const CARPETA_AUTH = resolve(import.meta.dirname, '..', '.auth');

/** Sesión del panel que guarda la preparación; vacía si no hay credenciales. */
export const RUTA_SESION = join(CARPETA_AUTH, 'personal.json');

export const MOTIVO_SIN_PERSONAL =
  'Sin usuario del panel: define E2E_USUARIO y E2E_CONTRASENA de un Administrador (y E2E_TOTP si ya tiene MFA).';

export interface Credenciales {
  usuario: string;
  contrasena: string;
}

export function credenciales(): Credenciales | null {
  const { E2E_USUARIO: usuario, E2E_CONTRASENA: contrasena } = process.env;
  return usuario && contrasena ? { usuario, contrasena } : null;
}

function rutaSecreto(usuario: string): string {
  return join(CARPETA_AUTH, `totp-${createHash('sha256').update(usuario).digest('hex').slice(0, 12)}.txt`);
}

/** Clave TOTP: E2E_TOTP o la que guardó la preparación al configurar MFA en local. */
export function secretoTotp(usuario: string): string | null {
  if (process.env.E2E_TOTP) return process.env.E2E_TOTP;
  const ruta = rutaSecreto(usuario);
  return existsSync(ruta) ? readFileSync(ruta, 'utf8').trim() : null;
}

function guardarSecreto(usuario: string, secreto: string): void {
  mkdirSync(CARPETA_AUTH, { recursive: true });
  writeFileSync(rutaSecreto(usuario), secreto, { encoding: 'utf8', mode: 0o600 });
}

export function guardarSesionVacia(): void {
  mkdirSync(CARPETA_AUTH, { recursive: true });
  writeFileSync(RUTA_SESION, JSON.stringify({ cookies: [], origins: [] }));
}

/** Configura la app autenticadora en /admin/configurar-mfa y devuelve la clave. */
export async function configurarMfa(page: Page): Promise<string> {
  await expect(page.getByRole('heading', { name: 'Configura la verificación en dos pasos' })).toBeVisible();
  // Identity la muestra en minúsculas y en grupos de 4. Nunca va en un mensaje: es un secreto.
  const clave = (await page.locator('code').first().innerText()).replace(/\s/g, '').toUpperCase();
  if (!/^[A-Z2-7]{16,}$/.test(clave)) throw new Error('La pantalla no mostró la clave para escribirla a mano.');
  await page.getByLabel('Código de 6 dígitos').fill(await codigoVigente(clave));
  await page.getByRole('button', { name: 'Activar' }).click();
  await expect(page.getByRole('heading', { name: 'Guarda tus códigos de recuperación' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Códigos de recuperación' }).getByRole('listitem')).not.toHaveCount(0);
  await page.getByRole('button', { name: 'Ya los guardé, continuar' }).click();
  return clave;
}

/**
 * Entra al panel por la interfaz: usuario y contraseña, y después el código MFA o su configuración.
 * Termina en la agenda del día.
 */
export async function entrarAlPanel(page: Page, cred: Credenciales, recordarClave = true): Promise<string> {
  await page.goto('/admin/entrar');
  await page.getByLabel('Usuario').fill(cred.usuario);
  await page.getByLabel('Contraseña').fill(cred.contrasena);
  await page.getByRole('button', { name: 'Entrar' }).click();

  const codigo = page.getByRole('heading', { name: 'Verificación en dos pasos' });
  const configurar = page.getByRole('heading', { name: 'Configura la verificación en dos pasos' });
  await expect(codigo.or(configurar)).toBeVisible();

  let secreto: string;
  if (await configurar.isVisible()) {
    secreto = await configurarMfa(page);
    if (recordarClave) guardarSecreto(cred.usuario, secreto);
  } else {
    const conocido = secretoTotp(cred.usuario);
    if (!conocido) {
      throw new Error(
        `${cred.usuario} ya tiene MFA y no se conoce su clave: define E2E_TOTP o restablece su MFA desde el panel.`,
      );
    }
    secreto = conocido;
    await page.getByLabel('Código de 6 dígitos').fill(await codigoVigente(secreto));
    await page.getByRole('button', { name: 'Verificar' }).click();
  }
  await expect(page.getByRole('heading', { name: 'Agenda del día' })).toBeVisible();
  return secreto;
}

/** Contexto de API con la sesión del personal que guardó la preparación. */
export async function apiPersonal(testInfo: TestInfo): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: testInfo.project.use.baseURL,
    ignoreHTTPSErrors: testInfo.project.use.ignoreHTTPSErrors,
    storageState: RUTA_SESION,
  });
}
