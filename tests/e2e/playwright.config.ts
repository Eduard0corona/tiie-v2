import { defineConfig, devices } from '@playwright/test';

// docs/12-pruebas.md#extremo-a-extremo. Contra QA en CI; en local, contra el sitio en https://localhost:7180.
const url = process.env.E2E_URL ?? 'https://localhost:7180';
const local = new URL(url).hostname === 'localhost';
// E2E_CANAL=msedge usa el Edge instalado en lugar del Chromium que descarga `npx playwright install`.
const canal = process.env.E2E_CANAL ? { channel: process.env.E2E_CANAL } : {};

export default defineConfig({
  testDir: './pruebas',
  timeout: 120_000,
  expect: { timeout: 15_000 },
  // Las pruebas comparten la base (plantillas, bloqueos, cupos): una a la vez, en orden.
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: url,
    // El certificado de desarrollo de ASP.NET Core no siempre es de confianza para el navegador de pruebas.
    ignoreHTTPSErrors: local,
    locale: 'es-MX',
    timezoneId: 'America/Mazatlan',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'preparacion', testMatch: /.*\.setup\.ts/, use: { ...canal } },
    {
      name: 'celular',
      use: { ...devices['Pixel 7'], ...canal },
      dependencies: ['preparacion'],
    },
    {
      name: 'escritorio',
      use: { ...devices['Desktop Chrome'], ...canal },
      dependencies: ['preparacion'],
    },
  ],
});
