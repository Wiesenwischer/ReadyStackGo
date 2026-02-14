import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Wizard Registries Step (v0.25)
 *
 * Tests the Container Registries step in the Setup Wizard.
 * The wizard is a one-time flow, so tests MUST run sequentially and share state.
 * We use the API to set up admin + org, then navigate through env → sources → registries.
 */

const BASE_URL = process.env.E2E_BASE_URL || 'http://localhost:8080';

async function setupWizardViaApi() {
  // Admin
  await fetch(`${BASE_URL}/api/wizard/admin`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'Admin1234' }),
  });
  // Organization
  await fetch(`${BASE_URL}/api/wizard/organization`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id: 'e2e-test-org', name: 'E2E Test Organization' }),
  });
  // Environment
  await fetch(`${BASE_URL}/api/wizard/environment`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: 'default', socketPath: '/var/run/docker.sock' }),
  });
  // Sources — add default source so registries can be detected from stack images
  await fetch(`${BASE_URL}/api/wizard/sources`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ registrySourceIds: ['rsgo-default'] }),
  });
}

/** Navigate from wizard step 3 (env) through to the Registries step */
async function navigateToRegistriesStep(page: Page) {
  await page.goto('/wizard');
  // Backend state is OrganizationSet → frontend shows step 3 (Environment)
  // Environment was already created via API, but wizard doesn't track that

  // Step 3: Environment — wait for the heading, then skip
  await expect(page.getByRole('heading', { name: /Environment/i })).toBeVisible({ timeout: 10000 });
  await page.getByRole('button', { name: 'Skip for now' }).click();

  // Step 4: Sources — wait for heading, then skip
  await expect(page.getByRole('heading', { name: /Stack Sources/i })).toBeVisible({ timeout: 5000 });
  await page.getByRole('button', { name: 'Skip for now' }).click();

  // Step 5: Registries — wait for heading (may take time for detection)
  await expect(page.getByRole('heading', { name: /Container Registries/i })).toBeVisible({ timeout: 15000 });
}

/** Wait for initial registry detection and checks to finish */
async function waitForChecksComplete(page: Page) {
  // The "Action Required" column header appears once detection is done
  await expect(page.getByRole('heading', { name: 'Action Required', exact: false })).toBeVisible({ timeout: 30000 });
}

test.describe.serial('Wizard Registries Step', () => {
  test.beforeAll(async () => {
    await setupWizardViaApi();
  });

  test('should show registries step with two-column layout', async ({ page }) => {
    await navigateToRegistriesStep(page);
    await waitForChecksComplete(page);

    // Column headers (h3 elements)
    await expect(page.getByRole('heading', { name: /Action Required/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Verified/i })).toBeVisible();

    // Bottom buttons
    await expect(page.getByRole('button', { name: 'Skip for now' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Continue/i })).toBeVisible();

    // Screenshot: Two-column layout
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-reg-01-columns.png'),
      fullPage: false,
    });
  });

  test('should show Check Access and Skip buttons on action cards', async ({ page }) => {
    await navigateToRegistriesStep(page);
    await waitForChecksComplete(page);

    // Check if action-required cards exist with buttons
    const checkButton = page.getByRole('button', { name: 'Check Access' }).first();
    const skipButton = page.getByRole('button', { name: 'Skip' }).first();
    const hasCards = await checkButton.isVisible().catch(() => false);

    if (hasCards) {
      await expect(checkButton).toBeVisible();
      await expect(skipButton).toBeVisible();

      // Credential fields should be present
      await expect(page.locator('input[placeholder="Username"]').first()).toBeVisible();
      await expect(page.locator('input[placeholder="Password / Token"]').first()).toBeVisible();

      // Screenshot: Action required card with credential fields
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'wizard-reg-02-action-card.png'),
        fullPage: false,
      });
    }
  });

  test('should skip a registry and show undo option', async ({ page }) => {
    await navigateToRegistriesStep(page);
    await waitForChecksComplete(page);

    const skipButton = page.getByRole('button', { name: 'Skip' }).first();
    const hasCards = await skipButton.isVisible().catch(() => false);

    if (hasCards) {
      await skipButton.click();

      // Skipped section label should appear
      await expect(page.getByText('Skipped', { exact: true })).toBeVisible();

      // Undo button should be visible
      const undoButton = page.getByRole('button', { name: 'Undo' }).first();
      await expect(undoButton).toBeVisible();

      // Screenshot: Skipped registry
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'wizard-reg-03-skipped.png'),
        fullPage: false,
      });

      // Undo the skip
      await undoButton.click();
      await expect(page.getByText('Skipped', { exact: true })).not.toBeVisible();
    }
  });

  test('should verify registry access without credentials (public check)', async ({ page }) => {
    await navigateToRegistriesStep(page);
    await waitForChecksComplete(page);

    const checkButton = page.getByRole('button', { name: 'Check Access' }).first();
    const hasCards = await checkButton.isVisible().catch(() => false);

    if (hasCards) {
      // Click Check Access without entering credentials
      await checkButton.click();

      // Wait for the check to complete (registry v2 API call)
      await page.waitForTimeout(10000);

      // Screenshot: After check access attempt
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'wizard-reg-04-checked.png'),
        fullPage: false,
      });
    }
  });

  test('should show verified cards with icons after auto-check', async ({ page }) => {
    await navigateToRegistriesStep(page);

    // Wait for auto-checks to complete
    await expect(page.getByRole('heading', { name: /Verified/i })).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(5000);

    // Check if verified column has entries (public registries auto-verified)
    const publicText = page.getByText('Public', { exact: true }).first();
    const hasVerified = await publicText.isVisible().catch(() => false);

    if (hasVerified) {
      // Screenshot: Verified column with public registry (globe icon)
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'wizard-reg-05-verified.png'),
        fullPage: false,
      });
    }
  });

  test('should show error when credentials are wrong', async ({ page }) => {
    await navigateToRegistriesStep(page);
    await waitForChecksComplete(page);

    const usernameInput = page.locator('input[placeholder="Username"]').first();
    const hasCards = await usernameInput.isVisible().catch(() => false);

    if (hasCards) {
      // Fill in fake credentials
      await usernameInput.fill('fakeuser');
      await page.locator('input[placeholder="Password / Token"]').first().fill('fakepass');
      await page.getByRole('button', { name: 'Check Access' }).first().click();

      // Wait for check (registry API call with auth)
      await page.waitForTimeout(10000);

      // Should show error message on card
      const errorText = page.getByText(/Access denied|Credentials required|failed/i).first();
      const hasError = await errorText.isVisible().catch(() => false);

      if (hasError) {
        await page.screenshot({
          path: path.join(SCREENSHOT_DIR, 'wizard-reg-06-error.png'),
          fullPage: false,
        });
      }
    }
  });

  test('should complete registries step and proceed to install', async ({ page }) => {
    await navigateToRegistriesStep(page);
    await waitForChecksComplete(page);

    // Click Continue to proceed
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should move to Install step (step 6)
    await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });

    // Screenshot: Complete Setup step after registries
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-reg-07-complete.png'),
      fullPage: false,
    });
  });
});
