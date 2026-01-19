import path from 'path';
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, '.', '');
    return {
      plugins: [react()],
      define: {
        'process.env.API_KEY': JSON.stringify(env.GEMINI_API_KEY),
        'process.env.GEMINI_API_KEY': JSON.stringify(env.GEMINI_API_KEY)
      },
      resolve: {
        alias: {
          '@': path.resolve(__dirname, '.'),
        }
      },
      server: {
        proxy: {
          '/api/v1': {
            target: 'http://127.0.0.1:8743',
            changeOrigin: true,
            secure: false
          }
        }
      },
      build: {
        outDir: path.resolve(__dirname, '../HyperV.Agent/wwwroot'),
        emptyOutDir: true,
      },
      base: '/'
    };
});
