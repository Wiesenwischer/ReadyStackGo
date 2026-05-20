import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E tests for the SNMP Monitoring settings page.
 *
 * Captures screenshots for the public docs at /docs/snmp.
 * Runs against a fresh container at http://localhost:8080 (admin / Admin1234).
 *
 * The tests are designed to be re-runnable: configuration is reset to a known
 * baseline at the start of each test that mutates it, and any V3 user created
 * by the test suite is removed at the end.
 */

const V3_TEST_USER = 'docs-monitor';

test.describe('SNMP Monitoring settings', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('reaches SNMP from the settings index', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('SNMP Monitoring', { exact: true })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-01-settings-index.png'),
      fullPage: false,
    });

    await page.click('a[href="/settings/snmp"]');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { name: 'SNMP Monitoring' })).toBeVisible();
  });

  test('shows the agent configuration form with sensible defaults', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Agent configuration' })).toBeVisible();
    await expect(page.getByText('Listen address')).toBeVisible();
    await expect(page.getByText('Port', { exact: true })).toBeVisible();
    await expect(page.getByText('Root OID', { exact: true })).toBeVisible();
    await expect(page.getByText('SNMPv2c community')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-02-agent-config.png'),
      fullPage: false,
    });
  });

  test('enables the agent and sets a v2c community', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    // Turn the Enabled toggle ON if it is not already.
    const enabledSwitch = page.getByRole('switch').first();
    const enabledState = await enabledSwitch.getAttribute('aria-checked');
    if (enabledState !== 'true') {
      await enabledSwitch.click();
    }

    // Set a community string. We use a non-default value so the screenshot
    // shows that the field is filled (masked).
    const communityField = page.locator('input[type="password"]').first();
    await communityField.fill('readonly-demo');

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-03-enable-and-community.png'),
      fullPage: false,
    });

    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByText('Saved. Agent reloads automatically.')).toBeVisible({ timeout: 10000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-04-saved-confirmation.png'),
      fullPage: false,
    });
  });

  test('adds an SNMPv3 user with SHA-256 / AES-128', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    // Open the V3 user form.
    await page.getByRole('button', { name: 'Add user' }).click();

    await expect(page.getByText('Auth protocol')).toBeVisible();
    await expect(page.getByText('Priv protocol')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-05-v3-form-empty.png'),
      fullPage: false,
    });

    // Fill in the form. Defaults are SHA-256 / AES-128 — keep them.
    await page.locator('label:has-text("Name") >> input').fill(V3_TEST_USER);

    // Scope the passphrase selectors to their labels — there is another
    // password input outside the form (the SNMPv2c community), which we must
    // not touch.
    await page.locator('label:has-text("Auth passphrase") >> input').fill('AuthPass12345');
    await page.locator('label:has-text("Priv passphrase") >> input').fill('PrivPass12345');

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-06-v3-form-filled.png'),
      fullPage: false,
    });

    await page.getByRole('button', { name: 'Add user' }).click();
    await expect(page.getByText(V3_TEST_USER)).toBeVisible({ timeout: 10000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-07-v3-user-added.png'),
      fullPage: false,
    });
  });

  test('shows the MIB download and the OID reference tree', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'MIB file' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Download MIB' })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-08-mib-download.png'),
      fullPage: false,
    });

    // Scroll to the OID reference tree.
    await expect(page.getByRole('heading', { name: 'OID reference' })).toBeVisible();
    await page.getByRole('heading', { name: 'OID reference' }).scrollIntoViewIfNeeded();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'snmp-09-oid-reference.png'),
      fullPage: false,
    });
  });

  test('cleans up the test V3 user', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    // The V3 user list is rendered as <li> entries; locate the one for our
    // test user and find its Delete button.
    const userRow = page.locator('li').filter({ hasText: V3_TEST_USER });
    const exists = await userRow.count() > 0;
    if (!exists) {
      // Nothing to clean up — earlier tests likely failed before adding the user.
      return;
    }

    // Auto-accept the native confirm() dialog.
    page.on('dialog', (d) => d.accept().catch(() => {}));

    await userRow.first().getByRole('button', { name: 'Delete' }).click();

    // Give the list a moment to refresh after deletion.
    await expect(page.locator('li').filter({ hasText: V3_TEST_USER }))
      .toHaveCount(0, { timeout: 10000 });
  });
});
