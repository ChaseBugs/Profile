# Capacitor + Vite Android 404 Fix Guide

## Problem Summary
- APK builds successfully but shows 404 on launch
- `Capacitor.isNativePlatform()` returns `false`
- App looks for remote assets instead of bundled files

## Root Causes & Solutions

### 1. Capacitor Config - Server Configuration (CRITICAL)

Even though you removed the server block, Capacitor 7.x **requires** proper server configuration for native platforms:

```typescript
// capacitor.config.ts
import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yourcompany.yourapp',
  appName: 'Your App Name',
  webDir: 'dist',
  server: {
    androidScheme: 'https',  // Use 'https' for Capacitor 7.x
    // DO NOT set url or hostname - this makes it look for remote server
  },
  plugins: {
    SplashScreen: {
      launchShowDuration: 2000,
    },
  },
};

export default config;
```

**Key Points:**
- `androidScheme: 'https'` is required for Capacitor 7.x
- **DO NOT** set `url` or `hostname` - this forces remote loading
- `webDir` should point to your Vite output directory (usually `dist`)

### 2. Vite Configuration

```typescript
// vite.config.ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'url';

export default defineConfig({
  plugins: [react()],
  base: './',  // Relative paths for bundled assets
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    // Ensure proper asset handling
    rollupOptions: {
      output: {
        assetFileNames: 'assets/[name].[hash][extname]',
        chunkFileNames: 'assets/[name].[hash].js',
        entryFileNames: 'assets/[name].[hash].js',
      },
    },
  },
  // Important for Capacitor
  publicDir: 'public',
});
```

### 3. MainActivity.java Configuration

Ensure your MainActivity properly initializes Capacitor:

```java
// android/app/src/main/java/.../MainActivity.java
package com.yourcompany.yourapp;

import android.os.Bundle;
import com.getcapacitor.BridgeActivity;
import com.getcapacitor.Plugin;

import java.util.ArrayList;

public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        
        // Initialize Capacitor plugins
        this.init(savedInstanceState, new ArrayList<Class<? extends Plugin>>() {{
            // Add your plugins here if needed
        }});
    }
}
```

**Critical:** Make sure you're extending `BridgeActivity`, not `Activity` or `AppCompatActivity`.

### 4. AndroidManifest.xml

Verify your manifest has proper permissions and activity configuration:

```xml
<!-- android/app/src/main/AndroidManifest.xml -->
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
    
    <application
        android:usesCleartextTraffic="true"
        ...>
        <activity
            android:name=".MainActivity"
            android:configChanges="orientation|keyboardHidden|keyboard|screenSize|locale|smallestScreenSize|screenLayout|uiMode"
            android:exported="true"
            android:launchMode="singleTask"
            android:theme="@style/AppTheme">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>
    </application>
</manifest>
```

### 5. Build Process

Your build command should be:

```bash
# Build Vite app
npm run build  # or: npx vite build --base=./

# Sync with Capacitor
npx cap sync android

# Build APK
cd android
./gradlew assembleDebug
# or for release:
./gradlew assembleRelease
```

### 6. Verify index.html Location

Ensure `index.html` is in your `dist` folder after build:

```
dist/
  ├── index.html          ← Must exist
  ├── assets/
  │   ├── index-[hash].js
  │   └── ...
  └── ...
```

### 7. Debugging Steps

Add this to your React app to debug:

```typescript
// In your main App.tsx or index.tsx
import { Capacitor } from '@capacitor/core';
import { useEffect } from 'react';

useEffect(() => {
  console.log('Platform:', Capacitor.getPlatform());
  console.log('Is Native:', Capacitor.isNativePlatform());
  console.log('Is Plugin Available:', Capacitor.isPluginAvailable('App'));
  console.log('Server URL:', window.location.href);
}, []);
```

### 8. Common Mistakes to Avoid

❌ **DON'T:**
- Set `server.url` in capacitor.config.ts (forces remote)
- Use `http://localhost` or any URL in server config
- Set `webDir` to a nested path like `dist/public`
- Use absolute paths in Vite (`base: '/'`)

✅ **DO:**
- Use `androidScheme: 'https'` in server config
- Use relative paths (`base: './'` in Vite)
- Ensure `webDir` points directly to `dist`
- Clear build cache: `npx cap clean android`

### 9. Complete Fix Checklist

- [ ] `capacitor.config.ts` has `server: { androidScheme: 'https' }` (no url/hostname)
- [ ] `vite.config.ts` has `base: './'`
- [ ] `webDir: 'dist'` (not `dist/public`)
- [ ] `index.html` exists in `dist/` after build
- [ ] MainActivity extends `BridgeActivity`
- [ ] Run `npx cap clean android` then `npx cap sync android`
- [ ] Rebuild APK from scratch

### 10. Nuclear Option - Complete Reset

If nothing works:

```bash
# Clean everything
rm -rf android
rm -rf dist
rm -rf node_modules/.vite

# Rebuild
npm run build
npx cap add android
npx cap sync android

# Rebuild APK
cd android && ./gradlew clean assembleDebug
```

## Expected Behavior After Fix

- `Capacitor.isNativePlatform()` returns `true`
- `Capacitor.getPlatform()` returns `'android'`
- App loads `index.html` from bundled assets
- No 404 errors on launch
- Assets load from `file://` or `capacitor://` protocol

