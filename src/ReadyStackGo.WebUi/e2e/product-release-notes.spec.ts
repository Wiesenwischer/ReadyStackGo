import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

// Fixed fake id for the stubbed product deployment.
const PD_ID = '11111111-2222-3333-4444-555555555555';

async function login(page: Page) {
  await page.goto('/login');
  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'Admin1234');
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard)?$/, { timeout: 15000 });
}

// A representative running product deployment at v1.0.0.
function deploymentResponse() {
  return {
    productDeploymentId: PD_ID,
    environmentId: 'env',
    productGroupId: 'com.example.demo',
    productId: 'stacks:demo:1.0.0',
    productName: 'demo',
    productDisplayName: 'Demo Product',
    productVersion: '1.0.0',
    deploymentName: 'demo-prod',
    status: 'Running',
    createdAt: '2026-06-17T08:00:00Z',
    continueOnError: false,
    totalStacks: 1,
    completedStacks: 1,
    failedStacks: 0,
    upgradeCount: 0,
    canRetry: false,
    canUpgrade: true,
    canRemove: true,
    canRedeploy: true,
    canStop: true,
    canRestart: true,
    canEnterMaintenance: true,
    canExitMaintenance: false,
    operationMode: 'Normal',
    stacks: [
      {
        stackName: 'web', stackDisplayName: 'web', stackId: 's1', status: 'Running',
        order: 0, serviceCount: 2, isNewInUpgrade: false,
      },
    ],
    sharedVariables: {},
  };
}

async function stubReleaseNotesApis(page: Page) {
  // Upgrade-check: a newer version with release notes is available.
  await page.route(`**/api/environments/*/product-deployments/${PD_ID}/upgrade/check`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        upgradeAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
        latestProductId: 'stacks:demo:1.1.0',
        latestHasReleaseNotes: true,
        latestReleaseNotesUrl: null,
        availableVersions: [
          { version: '1.1.0', productId: 'stacks:demo:1.1.0', sourceId: 'stacks', stackCount: 1, hasReleaseNotes: true },
        ],
        canUpgrade: true,
      }),
    }),
  );

  // Release notes for v1.1.0: markdown (own CHANGELOG.md).
  await page.route(`**/api/environments/*/product-deployments/${PD_ID}/release-notes**`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        mode: 'markdown',
        version: '1.1.0',
        content:
          '# Demo Product 1.1.0\n\n' +
          '## Added\n- New dashboard widget for live metrics\n- Configurable retention policy\n\n' +
          '## Fixed\n- Stability of the background sync worker\n\n' +
          '## Changed\n- Updated base image to the latest LTS',
      }),
    }),
  );

  // Detail page data (API path only — must not catch the SPA route navigation).
  await page.route(`**/api/environments/*/product-deployments/${PD_ID}`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(deploymentResponse()) }),
  );
}

test.describe('Product release notes', () => {
  test('update-available badge on the product deployment detail page', async ({ page }) => {
    await login(page);
    await stubReleaseNotesApis(page);

    await page.goto(`/product-deployments/${PD_ID}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/Update available: v1\.1\.0/i)).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'product-release-notes-01-badge.png'), fullPage: false });
  });

  test('release notes viewer renders the changelog', async ({ page }) => {
    await login(page);
    await stubReleaseNotesApis(page);

    await page.goto(`/product-deployments/${PD_ID}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Release notes' }).click();
    await expect(page.getByText('Release notes — v1.1.0')).toBeVisible();
    await expect(page.getByText('Demo Product 1.1.0')).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'product-release-notes-02-viewer.png'), fullPage: false });
  });
});
