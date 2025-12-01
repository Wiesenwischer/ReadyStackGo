import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright E2E Test Configuration for Container/Production testing
 * This runs tests against the Docker container, which is how the app is deployed.
 *
 * Usage:
 *   1. Start container: docker run -d -p 8080:8080 -v $(pwd)/config:/app/config wiesenwischer/readystackgo:latest
 *   2. Run tests: npx playwright test --config=playwright.container.config.ts
 *
 * Or with custom URL:
 *   E2E_BASE_URL=http://test-ux-dokker:8080 npx playwright test --config=playwright.container.config.ts
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup-container.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    // In container mode, everything runs on single port
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:8080',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // No webServer config - container must be started externally
  // This ensures we test the actual production deployment
});
