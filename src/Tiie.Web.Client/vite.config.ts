import { fileURLToPath, URL } from 'node:url';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// El backend corre en https://localhost:7180 con el certificado de desarrollo de .NET.
const backend = { target: 'https://localhost:7180', secure: false };

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  build: {
    outDir: '../Tiie.Web/wwwroot',
    emptyOutDir: true,
    sourcemap: 'hidden',
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': backend,
      '^/mi-cita/.+': backend, // el intercambio del token lo hace el servidor
    },
  },
});
