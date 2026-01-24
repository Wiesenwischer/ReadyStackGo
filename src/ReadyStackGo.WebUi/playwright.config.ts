import { defineConfig, devices } from '@playwright/test';

// In CI, the working directory is the repo root
const isCI = !!process.env.CI;

/**
 * Playwright E2E Test Configuration für ReadyStackGo
 * Siehe https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0, // Reduced from 2 to 1 retry to speed up CI
  workers: isCI ? 1 : undefined,
  reporter: 'html',
  timeout: 60 * 1000, // 60 seconds per test (default is 30s)
  expect: {
    timeout: 10 * 1000, // 10 seconds for expect assertions (default is 5s)
  },
  use: {
    baseURL: 'http://localhost:5174',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    actionTimeout: 15 * 1000, // 15 seconds for actions like click, fill
    navigationTimeout: 30 * 1000, // 30 seconds for navigation
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  /* Development server für E2E tests */
  // Note: Backend runs in Docker via global-setup.ts
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5174',
    reuseExistingServer: !isCI,
    timeout: 180 * 1000, // 3 minutes for frontend
  },
});
