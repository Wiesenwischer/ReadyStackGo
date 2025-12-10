import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'
import svgr from 'vite-plugin-svgr'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    svgr({
      svgrOptions: {
        icon: true,
      },
    }),
  ],
  server: {
    port: 5174,
    host: '127.0.0.1', // Explicitly bind to IPv4 for Playwright compatibility
    proxy: {
      '/api': {
        target: 'http://localhost:5259',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: '../ReadyStackGo.Api/wwwroot',
    emptyOutDir: true,
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
})
