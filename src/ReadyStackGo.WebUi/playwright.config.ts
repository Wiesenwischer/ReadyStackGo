import { defineConfig, devices } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Test config directory path
const testConfigDir = path.resolve(__dirname, '..', 'ReadyStackGo.Api', 'config');

/**
 * Playwright E2E Test Configuration für ReadyStackGo
 * Siehe https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
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
      env: {
        ConfigPath: testConfigDir,
      },
    },
  ],
});
