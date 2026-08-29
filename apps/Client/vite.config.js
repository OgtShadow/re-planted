import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react-swc'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  
  return {
    plugins: [react()],
    server: {
      host: '0.0.0.0',
      port: parseInt(env.VITE_PORT) || 5173,
      hmr: {
        protocol: 'ws',
        host: 'localhost',
        port: parseInt(env.VITE_PORT) || 5173,
      },
      watch: {
        usePolling: true, // wymagane dla Docker na Windows (WSL2/Hyper-V volumes)
      },
    },
  }
})
