import { defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config.ts';

// Las pruebas corren en UTC, como CI y como cualquier navegador fuera de Sinaloa: el código debe usar
// src/lib/fechas.ts y no la zona de la máquina. Se fija aquí para que los procesos de Vitest la hereden.
process.env.TZ = 'UTC';

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      restoreMocks: true,
      // Los flujos completos (asistente de reserva) recorren cuatro pantallas con carga perezosa.
      testTimeout: 15_000,
      // Con un trabajador por núcleo, la primera carga perezosa de cada archivo compite por las transformaciones
      // de Vite y pasa de 10 s en equipos de muchos núcleos; con 4 la suite completa es estable.
      maxWorkers: 4,
    },
  }),
);
