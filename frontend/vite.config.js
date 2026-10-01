import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Proxy /api to the ASP.NET Core backend so the browser sees a single origin in dev.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5085',
        changeOrigin: true
      }
    }
  }
});
