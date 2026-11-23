import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', testMatch: 'transactions.spec.ts', workers: 2, timeout: 60000,
  reporter: [['list'], ['json', { outputFile: '../docs/evidence/phase02c/e2e-results.json' }]],
  use: { baseURL: 'http://127.0.0.1:8083', screenshot: 'off', trace: 'off' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['iPhone 13'], viewport: { width: 390, height: 844 }, defaultBrowserType: 'chromium' } },
  ],
});
