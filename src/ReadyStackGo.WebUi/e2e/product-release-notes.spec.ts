import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

// Catalog product id (contains colons, like real source-scoped ids) and its deployment.
const PRODUCT_ID = 'stacks:demo:1.1.0';
const PD_ID = '11111111-2222-3333-4444-555555555555';

async function login(page: Page) {
  await page.goto('/login');
  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'Admin1234');
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard)?$/, { timeout: 15000 });
}

// ---- Stub markdown (English + German), including a GFM table to prove table rendering. ----
const EN_MARKDOWN =
  '# Demo Product 1.1.0\n\n' +
  '## Highlights\n\n' +
  '| Area | Change |\n' +
  '|------|--------|\n' +
  '| Dashboard | New live metrics widget |\n' +
  '| Sync | More stable background worker |\n\n' +
  '## Added\n- Configurable retention policy\n\n' +
  '## Fixed\n- Background sync stability';

const DE_MARKDOWN =
  '# Demo Product 1.1.0\n\n' +
  '## Höhepunkte\n\n' +
  '| Bereich | Änderung |\n' +
  '|---------|----------|\n' +
  '| Dashboard | Neues Live-Metrik-Widget |\n' +
  '| Sync | Stabilerer Hintergrund-Worker |\n\n' +
  '## Hinzugefügt\n- Konfigurierbare Aufbewahrungsrichtlinie';

interface ReleaseNotesStub {
  mode?: 'markdown' | 'url' | 'none';
  availableLocales?: string[];
  url?: string;
}

/**
 * Stubs the catalog release-notes endpoint (`GET /api/products/{id}/release-notes?locale=`).
 * Serves German or English markdown based on the requested locale so the language selector
 * can be exercised deterministically.
 */
async function stubCatalogReleaseNotes(page: Page, opts: ReleaseNotesStub = {}) {
  const { mode = 'markdown', availableLocales = ['de', 'en'], url } = opts;

  await page.route(/\/api\/products\/[^/]+\/release-notes/, (route) => {
    const requested = new URL(route.request().url()).searchParams.get('locale');
    const locale = requested === 'de' ? 'de' : 'en';

    const body =
      mode === 'markdown'
        ? { success: true, mode, version: '1.1.0', locale, availableLocales, content: locale === 'de' ? DE_MARKDOWN : EN_MARKDOWN }
        : mode === 'url'
          ? { success: true, mode, version: '1.1.0', url: url ?? 'https://example.com/releases/1.1.0' }
          : { success: true, mode: 'none', version: '1.1.0' };

    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
}

/** Stubs the catalog product detail so the Stack-Catalog "Release notes" entry point renders. */
async function stubCatalogProduct(page: Page) {
  // getProduct — matches /api/products/{id} but NOT /api/products/{id}/release-notes.
  await page.route(/\/api\/products\/[^/?]+$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: PRODUCT_ID,
        groupId: 'com.example.demo',
        sourceId: 'stacks',
        sourceName: 'Local Stacks',
        name: 'Demo Product',
        description: 'A demo product used for documentation screenshots.',
        isMultiStack: false,
        totalServices: 2,
        totalVariables: 0,
        stacks: [{ id: 's1', name: 'web', services: ['web', 'db'], variables: [] }],
        lastSyncedAt: '2026-07-01T08:00:00Z',
        availableVersions: [
          { version: '1.1.0', productId: PRODUCT_ID, defaultStackId: 's1', isCurrent: true, hasReleaseNotes: true },
        ],
      }),
    }),
  );

  // No active deployment for this catalog product — the store swallows this (catalog-only).
  await page.route('**/product-deployments/by-product/**', (route) =>
    route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ success: false }) }),
  );
}

/** Stubs the deployment + upgrade-check so the "update available" badge (a second entry point) renders. */
async function stubDeploymentWithUpgrade(page: Page) {
  await page.route(`**/api/environments/*/product-deployments/${PD_ID}/upgrade/check`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        upgradeAvailable: true,
        currentVersion: '1.0.0',
        latestVersion: '1.1.0',
        latestProductId: PRODUCT_ID,
        latestHasReleaseNotes: true,
        latestReleaseNotesUrl: null,
        availableVersions: [{ version: '1.1.0', productId: PRODUCT_ID, sourceId: 'stacks', stackCount: 1, hasReleaseNotes: true }],
        canUpgrade: true,
      }),
    }),
  );

  await page.route(`**/api/environments/*/product-deployments/${PD_ID}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        productDeploymentId: PD_ID, environmentId: 'env', productGroupId: 'com.example.demo',
        productId: 'stacks:demo:1.0.0', productName: 'demo', productDisplayName: 'Demo Product',
        productVersion: '1.0.0', deploymentName: 'demo-prod', status: 'Running',
        createdAt: '2026-06-17T08:00:00Z', continueOnError: false, totalStacks: 1, completedStacks: 1,
        failedStacks: 0, upgradeCount: 0, canRetry: false, canUpgrade: true, canRemove: true,
        canRedeploy: true, canStop: true, canRestart: true, canEnterMaintenance: true,
        canExitMaintenance: false, operationMode: 'Normal',
        stacks: [{ stackName: 'web', stackDisplayName: 'web', stackId: 's1', status: 'Running', order: 0, serviceCount: 2, isNewInUpgrade: false }],
        sharedVariables: {},
      }),
    }),
  );
}

test.describe('Product release notes', () => {
  test('Stack Catalog exposes a "Release notes" button on a product with a changelog', async ({ page }) => {
    await login(page);
    await stubCatalogProduct(page);
    await stubCatalogReleaseNotes(page);

    await page.goto(`/catalog/${encodeURIComponent(PRODUCT_ID)}`);
    await page.waitForLoadState('networkidle');

    const releaseNotesButton = page.getByRole('button', { name: 'Release notes' });
    await expect(releaseNotesButton).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'product-release-notes-01-catalog-button.png'), fullPage: false });
  });

  test('dedicated page renders the changelog markdown including GFM tables', async ({ page }) => {
    await login(page);
    await stubCatalogReleaseNotes(page);

    await page.goto(`/release-notes/${encodeURIComponent(PRODUCT_ID)}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Release notes — v1.1.0' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Demo Product 1.1.0' })).toBeVisible();

    // GFM table must render as a real table, not raw pipes (regression #458).
    const table = page.locator('.prose table');
    await expect(table).toBeVisible();
    await expect(table.getByText('New live metrics widget')).toBeVisible();

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'product-release-notes-02-page.png'), fullPage: false });
  });

  test('language selector switches locale and reflects it in the URL', async ({ page }) => {
    await login(page);
    await stubCatalogReleaseNotes(page, { availableLocales: ['de', 'en'] });

    await page.goto(`/release-notes/${encodeURIComponent(PRODUCT_ID)}?locale=en`);
    await page.waitForLoadState('networkidle');

    // Both language buttons are offered when >1 localized changelog exists.
    // exact:true — otherwise "EN"/"DE" match substrings like "Environment"/"Deployments".
    await expect(page.getByRole('button', { name: 'DE', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'EN', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'DE', exact: true }).click();
    await page.waitForURL(/locale=de/);
    await expect(page.getByRole('heading', { name: 'Höhepunkte' })).toBeVisible();

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'product-release-notes-03-language.png'), fullPage: false });
  });

  test('update-available badge links to the release notes page', async ({ page }) => {
    await login(page);
    await stubDeploymentWithUpgrade(page);
    await stubCatalogReleaseNotes(page);

    await page.goto(`/product-deployments/${PD_ID}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/Update available: v1\.1\.0/i)).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'product-release-notes-04-update-badge.png'), fullPage: false });

    // The badge's "Release notes" action navigates to the dedicated page.
    await page.getByRole('button', { name: 'Release notes' }).click();
    await page.waitForURL(/\/release-notes\//);
    await expect(page.getByRole('heading', { name: 'Release notes — v1.1.0' })).toBeVisible();
  });

  // ---- Edge cases (no screenshots) ----

  test('shows an empty state when a version has no release notes', async ({ page }) => {
    await login(page);
    await stubCatalogReleaseNotes(page, { mode: 'none' });

    await page.goto(`/release-notes/${encodeURIComponent(PRODUCT_ID)}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('No release notes available.')).toBeVisible();
    await expect(page.locator('.prose table')).toHaveCount(0);
  });

  test('externally hosted notes are linked, never embedded', async ({ page }) => {
    await login(page);
    await stubCatalogReleaseNotes(page, { mode: 'url', url: 'https://example.com/releases/1.1.0' });

    await page.goto(`/release-notes/${encodeURIComponent(PRODUCT_ID)}`);
    await page.waitForLoadState('networkidle');

    const link = page.getByRole('link', { name: 'https://example.com/releases/1.1.0' });
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(page.locator('.prose')).toHaveCount(0);
  });

  test('hides the language selector when only one locale exists', async ({ page }) => {
    await login(page);
    await stubCatalogReleaseNotes(page, { availableLocales: ['en'] });

    await page.goto(`/release-notes/${encodeURIComponent(PRODUCT_ID)}?locale=en`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Release notes — v1.1.0' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'EN', exact: true })).toHaveCount(0);
  });
});
