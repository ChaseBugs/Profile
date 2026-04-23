# Quick Diagnostic Checklist

## Immediate Fix (Most Likely Solution)

The issue is almost certainly in your `capacitor.config.ts`. For Capacitor 7.x:

**WRONG (causes 404):**
```typescript
server: {
  // Empty or missing androidScheme
}
// OR
server: {
  url: 'http://localhost:5173'  // ❌ Forces remote lookup
}
```

**CORRECT:**
```typescript
server: {
  androidScheme: 'https'  // ✅ Required for Capacitor 7.x
}
```

## Step-by-Step Fix

1. **Update capacitor.config.ts:**
   ```typescript
   webDir: 'dist',  // Not 'dist/public'
   server: {
     androidScheme: 'https',
   },
   ```

2. **Verify vite.config.ts:**
   ```typescript
   base: './',  // Relative paths
   ```

3. **Clean and rebuild:**
   ```bash
   npx cap clean android
   npm run build
   npx cap sync android
   cd android && ./gradlew clean assembleDebug
   ```

4. **Verify dist/index.html exists** after build

## Why This Happens

- Capacitor 7.x changed how it handles local file loading
- Without `androidScheme: 'https'`, it may try to load from a remote server
- Setting `url` or `hostname` in server config forces remote mode
- Wrong `webDir` path means it can't find your built files

## Test After Fix

In your React app, add this temporarily:
```typescript
console.log('Platform:', Capacitor.getPlatform());
console.log('Is Native:', Capacitor.isNativePlatform());
console.log('URL:', window.location.href);
```

Expected output:
- Platform: `'android'`
- Is Native: `true`
- URL: Should start with `capacitor://` or `https://` (not `http://localhost`)

