import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for CI/CD Integration Settings (API Key Management)
 * Tests the full API key lifecycle and captures screenshots for documentation.
 */

test.describe('CI/CD Settings - API Key Management', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should navigate to CI/CD settings from Settings page', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // CI/CD Integration card should be visible
    await expect(page.getByText('CI/CD Integration')).toBeVisible();
    await expect(page.getByText('Manage API keys for automated deployments')).toBeVisible();

    // Screenshot: Settings page with CI/CD tab
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'cicd-01-settings-nav.png'),
      fullPage: false
    });

    // Navigate to CI/CD
    await page.click('a[href="/settings/cicd"]');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'CI/CD Integration' })).toBeVisible();
  });

  test('should show empty state when no keys exist', async ({ page }) => {
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    // Should show empty state or key list
    const emptyState = page.getByText('No API keys yet');
    const keyTable = page.locator('table');

    const hasEmpty = await emptyState.isVisible().catch(() => false);
    const hasTable = await keyTable.isVisible().catch(() => false);

    // One of these should be visible
    expect(hasEmpty || hasTable).toBe(true);

    // Screenshot: CI/CD page (empty state or list)
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'cicd-02-empty-state.png'),
      fullPage: false
    });
  });

  test('should create an API key with all options', async ({ page }) => {
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    // Click Create API Key
    const createButton = page.getByRole('button', { name: /Create API Key/i });
    await expect(createButton).toBeVisible();
    await createButton.click();

    // Modal should appear
    await expect(page.getByRole('heading', { name: 'Create API Key' })).toBeVisible();
    await expect(page.getByText('The API key will be shown only once')).toBeVisible();

    // Fill in form
    await page.fill('input[placeholder*="GitHub Actions"]', 'Azure DevOps Deploy');

    // Check all permissions
    const checkboxes = page.locator('input[type="checkbox"]');
    const count = await checkboxes.count();
    for (let i = 0; i < count; i++) {
      if (!(await checkboxes.nth(i).isChecked())) {
        await checkboxes.nth(i).check();
      }
    }

    // Select environment if available
    const envSelect = page.locator('select');
    const optionCount = await envSelect.locator('option').count();
    if (optionCount > 1) {
      await envSelect.selectOption({ index: 1 });
    }

    // Set expiry date (90 days from now)
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + 90);
    await page.fill('input[type="date"]', futureDate.toISOString().split('T')[0]);

    // Screenshot: Filled create modal
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'cicd-03-create-modal.png'),
      fullPage: false
    });

    // Submit
    await page.click('button[type="submit"]:has-text("Create")');

    // Should show the created key banner
    await expect(page.getByText('API key created')).toBeVisible({ timeout: 5000 });

    // Key should start with rsgo_
    const keyCode = page.locator('code');
    await expect(keyCode).toBeVisible();
    const keyText = await keyCode.textContent();
    expect(keyText).toMatch(/^rsgo_/);
    expect(keyText!.length).toBeGreaterThan(36); // rsgo_ + 32 chars

    // Screenshot: Key created banner
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'cicd-04-key-created.png'),
      fullPage: false
    });

    // Copy button should work
    const copyButton = page.getByRole('button', { name: /Copy/i });
    await expect(copyButton).toBeVisible();

    // Dismiss the banner
    const closeButtons = page.locator('.bg-green-50 button svg, [class*="bg-green"] button svg').last();
    if (await closeButtons.isVisible().catch(() => false)) {
      await closeButtons.click();
      await page.waitForTimeout(500);
    }

    // Key should now appear in the table
    await expect(page.locator('table')).toBeVisible();
    await expect(page.getByText('Azure DevOps Deploy')).toBeVisible();

    // Screenshot: Key list with new key
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'cicd-05-key-list.png'),
      fullPage: false
    });
  });

  test('should show permissions in key list', async ({ page }) => {
    // First create a key
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();
    await page.fill('input[placeholder*="GitHub Actions"]', 'Permission Test Key');
    // Only Redeploy should be checked by default
    await page.click('button[type="submit"]:has-text("Create")');
    await expect(page.getByText('API key created')).toBeVisible({ timeout: 5000 });

    // Dismiss banner
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    // Should show Redeploy permission badge
    const row = page.locator('tr', { hasText: 'Permission Test Key' });
    await expect(row.getByText('Redeploy')).toBeVisible();
  });

  test('should require name to create key', async ({ page }) => {
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();

    // Try to submit without name - HTML5 validation should prevent
    const submitButton = page.locator('button[type="submit"]:has-text("Create")');
    await submitButton.click();

    // Should still be in the modal (form didn't submit)
    await expect(page.getByRole('heading', { name: 'Create API Key' })).toBeVisible();
  });

  test('should disable create button when no permissions selected', async ({ page }) => {
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();
    await page.fill('input[placeholder*="GitHub Actions"]', 'No Perms Key');

    // Uncheck the default permission
    const checkboxes = page.locator('input[type="checkbox"]');
    const count = await checkboxes.count();
    for (let i = 0; i < count; i++) {
      if (await checkboxes.nth(i).isChecked()) {
        await checkboxes.nth(i).uncheck();
      }
    }

    // Submit button should be disabled
    const submitButton = page.locator('button[type="submit"]:has-text("Create")');
    await expect(submitButton).toBeDisabled();
  });

  test('should revoke an API key', async ({ page }) => {
    // Create a key first
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();
    await page.fill('input[placeholder*="GitHub Actions"]', 'Key To Revoke');
    await page.click('button[type="submit"]:has-text("Create")');
    await expect(page.getByText('API key created')).toBeVisible({ timeout: 5000 });

    // Navigate back to refresh
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    // Find the revoke button for our key
    const row = page.locator('tr', { hasText: 'Key To Revoke' });
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: 'Revoke' }).click();

    // Revoke modal should appear
    await expect(page.getByRole('heading', { name: 'Revoke API Key' })).toBeVisible();
    await expect(page.getByRole('strong')).toContainText('Key To Revoke');
    await expect(page.getByText('This action cannot be undone')).toBeVisible();

    // Optionally add reason
    await page.fill('input[placeholder*="compromised"]', 'No longer needed');

    // Confirm revoke
    await page.getByRole('button', { name: /Revoke Key/i }).click();
    await page.waitForTimeout(1000);

    // Key should now show Revoked status
    const revokedRow = page.locator('tr', { hasText: 'Key To Revoke' });
    await expect(revokedRow.getByText('Revoked', { exact: true })).toBeVisible();
  });

  test('should cancel create modal', async ({ page }) => {
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();
    await expect(page.getByRole('heading', { name: 'Create API Key' })).toBeVisible();

    // Click Cancel
    await page.getByRole('button', { name: 'Cancel' }).click();

    // Modal should be gone
    await expect(page.getByRole('heading', { name: 'Create API Key' })).not.toBeVisible();
  });

  test('should cancel revoke modal', async ({ page }) => {
    // Create a key first
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();
    await page.fill('input[placeholder*="GitHub Actions"]', 'Cancel Revoke Key');
    await page.click('button[type="submit"]:has-text("Create")');
    await expect(page.getByText('API key created')).toBeVisible({ timeout: 5000 });

    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    const row = page.locator('tr', { hasText: 'Cancel Revoke Key' });
    await row.getByRole('button', { name: 'Revoke' }).click();

    await expect(page.getByRole('heading', { name: 'Revoke API Key' })).toBeVisible();

    // Click Cancel
    await page.getByRole('button', { name: 'Cancel' }).click();

    // Key should still be Active
    await expect(row.getByText('Active')).toBeVisible();
  });

  test('should not show revoke button for already revoked keys', async ({ page }) => {
    // Create and revoke a key
    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Create API Key/i }).click();
    await page.fill('input[placeholder*="GitHub Actions"]', 'Already Revoked Key');
    await page.click('button[type="submit"]:has-text("Create")');
    await expect(page.getByText('API key created')).toBeVisible({ timeout: 5000 });

    await page.goto('/settings/cicd');
    await page.waitForLoadState('networkidle');

    const row = page.locator('tr', { hasText: 'Already Revoked Key' });
    await row.getByRole('button', { name: 'Revoke' }).click();
    await page.getByRole('button', { name: /Revoke Key/i }).click();
    await page.waitForTimeout(1000);

    // After revoking, the revoke button should not be visible in that row
    const revokedRow = page.locator('tr', { hasText: 'Already Revoked Key' });
    await expect(revokedRow.getByText('Revoked', { exact: true })).toBeVisible();
    await expect(revokedRow.getByRole('button', { name: 'Revoke' })).not.toBeVisible();
  });
});
