import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  
  // CRITICAL: Use relative paths for Capacitor
  base: './',
  
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    // Ensure proper asset handling for mobile
    rollupOptions: {
      output: {
        assetFileNames: 'assets/[name].[hash][extname]',
        chunkFileNames: 'assets/[name].[hash].js',
        entryFileNames: 'assets/[name].[hash].js',
      },
    },
    // Increase chunk size warning limit for mobile apps
    chunkSizeWarningLimit: 1000,
  },
  
  // Public directory for static assets
  publicDir: 'public',
  
  // Optional: Optimize for production
  optimizeDeps: {
    include: ['react', 'react-dom'],
  },
});

