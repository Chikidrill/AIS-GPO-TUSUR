import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '')

  return {
    plugins: [vue()],

    server: {
      host: '0.0.0.0',
      port: 5173,
      strictPort: true,

      watch: {
        usePolling: env.VITE_USE_POLLING === 'true',
      },

      proxy: {
        '/api': {
          target: env.API_PROXY_TARGET || 'http://localhost:5080',
          changeOrigin: true,
        },
      },
    },
  }
})