import { defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config.ts';

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
