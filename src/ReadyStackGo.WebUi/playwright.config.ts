import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright E2E Test Configuration für ReadyStackGo
 * Siehe https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:5174',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  /* Development server für E2E tests */
  webServer: [
    {
      command: 'npm run dev',
      url: 'http://localhost:5174',
      reuseExistingServer: !process.env.CI,
      timeout: 120 * 1000,
    },
    {
      command: 'cd ../../src/ReadyStackGo.Api && dotnet run',
      url: 'http://localhost:5259/api/containers',
      reuseExistingServer: !process.env.CI,
      timeout: 120 * 1000,
    },
  ],
});
