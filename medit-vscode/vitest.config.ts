import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    include: ['src/test/**/*.test.ts', 'webview/src/**/*.test.tsx'],
    exclude: ['src/test/integration/**'],
    environment: 'node',
    globals: true,
    environmentMatchGlobs: [['webview/src/**', 'happy-dom']],
  },
});
