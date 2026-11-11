import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './e2e',
  reporter: [['list'], ['json', { outputFile: '../docs/evidence/phase00b/e2e-results.json' }]],
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:8080', screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } },
    { name: 'mobile', use: { ...devices['iPhone 13'], viewport: { width: 390, height: 844 }, defaultBrowserType: 'chromium' } },
    { name: 'compact', use: { viewport: { width: 320, height: 740 } } },
  ],
});
