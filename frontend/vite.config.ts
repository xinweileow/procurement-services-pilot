/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Frontend/src/api/client.ts calls window.location.origin + '/api/v1' — proxy dev-server
    // requests through to the ASP.NET Core API (launchSettings.json's http profile) so that
    // stays same-origin instead of 404ing against Vite itself.
    proxy: {
      '/api': 'http://localhost:5075',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    globals: true,
  },
})
