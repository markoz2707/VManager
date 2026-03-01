import path from 'path';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:7226',
        secure: false,
        changeOrigin: true,
      },
      '/hubs': {
        target: 'https://localhost:7226',
        secure: false,
        ws: true,
        changeOrigin: true,
      },
    },
  },
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
});
