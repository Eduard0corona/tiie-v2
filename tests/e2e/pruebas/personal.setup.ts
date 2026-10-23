import { expect, test as preparacion } from '@playwright/test';
import { credenciales, entrarAlPanel, guardarSesionVacia, MOTIVO_SIN_PERSONAL, RUTA_SESION } from '../ayudas/personal';

// Una sola entrada al panel para todas las pruebas del personal: el límite de intentos de entrada es de 10
// por minuto por IP (docs/11#límites-de-solicitudes).
preparacion('sesión del personal', async ({ page }) => {
  const cred = credenciales();
  if (!cred) {
    guardarSesionVacia();
    preparacion.skip(true, MOTIVO_SIN_PERSONAL);
    return;
  }

  await entrarAlPanel(page, cred);
  const yo = await page.request.get('/api/admin/auth/yo');
  expect(yo.ok()).toBeTruthy();
  const { rol, mfaActivo } = (await yo.json()) as { rol: string | null; mfaActivo: boolean };
  expect(mfaActivo).toBe(true);
  // Las pruebas de plantillas, bloqueos y usuarios necesitan el rol más alto.
  expect(rol, `${cred.usuario} debe ser Administrador para las pruebas del panel.`).toBe('Administrador');
  await page.context().storageState({ path: RUTA_SESION });
});
