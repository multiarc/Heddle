import { defineConfig, devices } from '@playwright/test';

// Phase 9 D10 — the demo smoke suite runs against the BUILT site (VitePress preview with the /Heddle/ base),
// not the dev server, so the deploy shape (base path, staged demo/ bundle) is exercised. Chromium only.
export default defineConfig({
  testDir: './demo-smoke',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: 'list',
  webServer: {
    command: 'npm run docs:preview -- --port 4173 --host 127.0.0.1',
    url: 'http://127.0.0.1:4173/Heddle/',
    timeout: 120_000,
    reuseExistingServer: !process.env.CI,
  },
  use: {
    baseURL: 'http://127.0.0.1:4173/Heddle/',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
