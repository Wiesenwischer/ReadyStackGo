import { defineConfig, devices } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Test config directory path (relative to repo root for CI, relative to WebUi for local)
const repoRoot = path.resolve(__dirname, '..', '..');
const testConfigDir = path.resolve(repoRoot, 'src', 'ReadyStackGo.Api', 'config');
const testDataDir = path.resolve(repoRoot, 'src', 'ReadyStackGo.Api', 'data');

// In CI, the working directory is the repo root
const isCI = !!process.env.CI;
const backendCommand = isCI
  ? `dotnet run --project ${path.resolve(repoRoot, 'src', 'ReadyStackGo.Api', 'ReadyStackGo.Api.csproj')} --configuration Release`
  : 'dotnet run --project ../ReadyStackGo.Api/ReadyStackGo.Api.csproj';

/**
 * Playwright E2E Test Configuration für ReadyStackGo
 * Siehe https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  workers: isCI ? 1 : undefined,
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
      reuseExistingServer: !isCI,
      timeout: 180 * 1000, // 3 minutes for frontend
    },
    {
      command: backendCommand,
      url: 'http://localhost:5259/health',
      reuseExistingServer: !isCI,
      timeout: 300 * 1000, // 5 minutes for backend startup in CI
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConfigPath: testConfigDir,
        DataPath: testDataDir,
      },
    },
  ],
});
