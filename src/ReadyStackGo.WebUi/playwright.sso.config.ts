import { defineConfig, devices } from '@playwright/test';

/**
 * Browser test of single sign-on against the container with the test identity provider
 * (scripts/sso-e2e.sh, docker-compose.sso-e2e.yml). Chromium resolves "test-idp" to 127.0.0.1,
 * so the browser and the ReadyStackGo container reach the provider under the same issuer.
 * The spec runs serially: it starts with a fresh installation (setup wizard with WYSCH).
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: ['**/sso-*.spec.ts'],
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  timeout: 5 * 60 * 1000,
  expect: { timeout: 15 * 1000 },
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    actionTimeout: 20 * 1000,
    navigationTimeout: 30 * 1000,
  },
  projects: [
    {
      name: 'sso',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 900 },
        launchOptions: { args: ['--host-resolver-rules=MAP test-idp 127.0.0.1'] },
      },
    },
  ],
});
