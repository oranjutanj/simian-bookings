import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  fullyParallel: false,
  reporter: [['list']],
  use: {
    headless: true,
    trace: 'retain-on-failure'
  },
  webServer: {
    command: 'func start',
    cwd: '../api',
    url: 'http://localhost:7071/api/session-types',
    reuseExistingServer: true,
    timeout: 120_000
  }
});
