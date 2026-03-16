import { defineConfig, devices } from '@playwright/test';

// In CI, the working directory is the repo root
const isCI = !!process.env.CI;

/**
 * Playwright E2E Test Configuration für ReadyStackGo
 *
 * Tests run against the Docker container on port 8080.
 * Container lifecycle is managed via global-setup/teardown.
 *
 * Note: onboarding.spec.ts is excluded from the default run because it requires
 * a fresh container (no admin created). Run it separately:
 *   docker compose down -v && docker compose up -d
 *   npx playwright test onboarding --config=playwright.container.config.ts
 */
export default defineConfig({
  testDir: './e2e',
  testIgnore: ['**/onboarding.spec.ts'],
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  workers: isCI ? 1 : undefined,
  reporter: isCI ? 'html' : 'list',
  timeout: 60 * 1000,
  expect: {
    timeout: 10 * 1000,
  },
  use: {
    baseURL: 'http://localhost:8080',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    actionTimeout: 15 * 1000,
    navigationTimeout: 30 * 1000,
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
