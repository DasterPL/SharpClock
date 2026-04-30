import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const DEVICE_URL = process.env.DEVICE_URL ?? 'http://192.168.1.100'

const API_PATHS = [
  '/modules',
  '/properties',
  '/screen',
  '/log',
  '/plugins',
  '/system',
]

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
    rollupOptions: {
      output: {
        entryFileNames: 'assets/[name]-[hash].js',
        chunkFileNames: 'assets/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash][extname]',
      },
    },
  },
  server: {
    proxy: Object.fromEntries(
      API_PATHS.map(p => [p, { target: DEVICE_URL, changeOrigin: true }])
    ),
  },
})
