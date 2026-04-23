import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yourcompany.yourapp',
  appName: 'Your App Name',
  webDir: 'dist',  // Direct path to dist, not dist/public
  server: {
    // CRITICAL: Use 'https' scheme for Capacitor 7.x
    androidScheme: 'https',
    // DO NOT set url or hostname - this forces remote server lookup
    // url: 'http://localhost:5173',  // ❌ REMOVE THIS
    // hostname: 'localhost',          // ❌ REMOVE THIS
  },
  plugins: {
    SplashScreen: {
      launchShowDuration: 2000,
      launchAutoHide: true,
    },
  },
};

export default config;

