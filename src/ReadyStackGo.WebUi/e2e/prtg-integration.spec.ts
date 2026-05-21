import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E spec that captures screenshots for the public PRTG integration docs.
 * Covers all four variants:
 *
 *   V1  Device Template Bundle    → Settings/SNMP "PRTG integration" card
 *   V3  Saved PrtgConnection      → Settings/PRTG Connections list + form
 *   V4  HTTP Data Advanced sensor → Settings/SNMP "PRTG HTTP sensor" card
 *   V2  Inline registration       → Deployment Detail "PRTG monitoring" card
 *                                   (requires an existing ProductDeployment —
 *                                   covered by a manual screenshot pass; the
 *                                   automated test here just makes sure the
 *                                   tabbed card layout renders cleanly on the
 *                                   settings flows we can fully script)
 *
 * Run via the temporary playwright config that points at the docker-compose
 * container on http://localhost:8080.
 */

const V3_TEST_CONNECTION = 'docs-screenshot-prtg';

test.describe('PRTG integration screenshots', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('captures the V1 Device Template Bundle card and the V4 HTTP sensor card on the SNMP settings page', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    // V4 — "PRTG HTTP sensor" card is at the top of the PRTG block.
    const httpCard = page.getByRole('heading', { name: 'PRTG HTTP sensor' });
    await expect(httpCard).toBeVisible();
    await httpCard.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v4-http-sensor-card.png'),
      fullPage: false,
    });

    // V1 — "PRTG integration" card with Download bundle button.
    const bundleCard = page.getByRole('heading', { name: 'PRTG integration' });
    await expect(bundleCard).toBeVisible();
    await bundleCard.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v1-bundle-card.png'),
      fullPage: false,
    });
  });

  test('V3 — settings index has the PRTG Connections tile', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    const tile = page.getByText('PRTG Connections', { exact: true });
    await expect(tile).toBeVisible();
    await tile.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-settings-tile.png'),
      fullPage: false,
    });
  });

  test('V3 — empty PRTG Connections list', async ({ page }) => {
    await page.goto('/settings/prtg-connections');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'PRTG Connections' })).toBeVisible();
    // Take the screenshot regardless of empty/populated — the page renders the
    // same way both ways, but the empty-state message is a docs talking-point.
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-connections-list.png'),
      fullPage: false,
    });
  });

  test('V3 — Add connection form', async ({ page }) => {
    await page.goto('/settings/prtg-connections');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: '+ Add connection' }).click();
    await expect(page.getByRole('heading', { name: 'New PRTG connection' })).toBeVisible();

    // Fill the form so the screenshot shows a realistic example.
    await page.locator('label:has-text("Name") >> input').first().fill(V3_TEST_CONNECTION);
    await page.locator('label:has-text("URL") >> input').first().fill('https://prtg.example.local');
    await page.locator('label:has-text("API token") >> input').first().fill('PRTG_TOKEN_demo_value');
    await page.locator('label:has-text("Template Device ID") >> input').first().fill('4221');
    // VerifyTLS checkbox: leave as default (true) for the screenshot.

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-add-form.png'),
      fullPage: false,
    });

    // Submit + capture the resulting populated list — useful as a second
    // screenshot for the docs.
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText(V3_TEST_CONNECTION).first()).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-connections-list-populated.png'),
      fullPage: false,
    });
  });

  test('V3 — cleanup: delete the demo connection so the suite is rerunnable', async ({ page }) => {
    // Best-effort cleanup. The native confirm() dialog occasionally outruns
    // playwright's dialog handler, so we make this test idempotent rather
    // than failing the whole suite on a hygiene step.
    await page.goto('/settings/prtg-connections');
    await page.waitForLoadState('networkidle');

    const row = page.locator('tr', { hasText: V3_TEST_CONNECTION });
    if (await row.count() === 0) {
      return; // nothing to clean up
    }

    page.on('dialog', (d) => d.accept().catch(() => {}));
    await row.first().getByRole('button', { name: 'Delete' }).click();

    try {
      await expect(page.locator('tr', { hasText: V3_TEST_CONNECTION }))
        .toHaveCount(0, { timeout: 5000 });
    } catch {
      // ignore — best effort, the next test run reuses the same name
    }
  });
});
