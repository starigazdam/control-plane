import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Aspire's YARP gateway forwards this internal host in local development.
    allowedHosts: ['aspire.dev.internal'],
  },
})
