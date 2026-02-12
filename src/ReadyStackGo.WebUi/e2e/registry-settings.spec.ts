import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * Helper to find a specific registry row by its name heading.
 * Navigates from the h5 element up to the row-level div.
 */
function registryRow(page: Page, name: string) {
  return page
    .locator('h5')
    .filter({ hasText: name })
    .locator('xpath=ancestor::div[contains(@class, "border-b")]')
    .first();
}

/**
 * E2E Tests for Container Registry Settings
 * Tests the full registry lifecycle and captures screenshots for documentation.
 */

test.describe('Container Registry Settings', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should navigate to Container Registries from Settings page', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Container Registries card should be visible
    await expect(page.getByText('Container Registries')).toBeVisible();
    await expect(page.getByText('Manage Docker registries for pulling container images')).toBeVisible();

    // Screenshot: Settings page with Container Registries card
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'registry-01-settings-nav.png'),
      fullPage: false
    });

    // Navigate to Container Registries
    await page.click('a[href="/settings/registries"]');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Container Registries' })).toBeVisible();
  });

  test('should show empty state when no registries exist', async ({ page }) => {
    await page.goto('/settings/registries');
    await page.waitForLoadState('networkidle');

    // Should show empty state or registry list
    const emptyState = page.getByText('No Registries Configured');
    const hasEmpty = await emptyState.isVisible().catch(() => false);

    if (hasEmpty) {
      await expect(page.getByText('Add container registries to manage authentication')).toBeVisible();
      await expect(page.getByRole('link', { name: /Add Your First Registry/i })).toBeVisible();
    }

    // Screenshot: Container Registries page (empty state or list)
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'registry-02-empty-state.png'),
      fullPage: false
    });
  });

  test('should add a Docker Hub registry with credentials', async ({ page }) => {
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    // Page heading should be visible
    await expect(page.getByRole('heading', { name: 'Add Container Registry' })).toBeVisible();

    // Docker Hub should be pre-selected
    const registrySelect = page.locator('select');
    await expect(registrySelect).toHaveValue('https://index.docker.io/v1/');

    // Name and URL should be pre-filled
    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await expect(nameInput).toHaveValue('Docker Hub');

    // Fill in credentials
    await page.fill('input[placeholder="username"]', 'myuser');
    await page.fill('input[placeholder="Enter password or token"]', 'mytoken123');

    // Add image patterns
    await page.fill('textarea', 'mycompany/*\nmycompany/**');

    // Screenshot: Filled add registry form
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'registry-03-add-form.png'),
      fullPage: false
    });

    // Submit
    await page.getByRole('button', { name: 'Create Registry' }).click();

    // Should redirect to registries list (use regex to avoid matching /add)
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // New registry should appear in the list
    const row = registryRow(page, 'Docker Hub');
    await expect(row).toBeVisible();
    await expect(row.getByText('Authenticated')).toBeVisible();

    // Screenshot: Registry list with new registry
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'registry-04-list-with-registry.png'),
      fullPage: false
    });
  });

  test('should add a GitHub Container Registry', async ({ page }) => {
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    // Select GHCR
    const registrySelect = page.locator('select');
    await registrySelect.selectOption('https://ghcr.io');

    // Name and URL should update
    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await expect(nameInput).toHaveValue('GitHub Container Registry');

    // Fill credentials
    await page.fill('input[placeholder="username"]', 'ghuser');
    await page.fill('input[placeholder="Enter password or token"]', 'ghp_token123');

    // Add patterns
    await page.fill('textarea', 'ghcr.io/myorg/**');

    // Submit
    await page.getByRole('button', { name: 'Create Registry' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Should show in list
    const row = registryRow(page, 'GitHub Container Registry');
    await expect(row).toBeVisible();
  });

  test('should set a registry as default', async ({ page }) => {
    // First ensure we have a registry
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await nameInput.clear();
    await nameInput.fill('Default Test Registry');

    await page.getByRole('button', { name: 'Create Registry' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Find the registry row and click "Set Default"
    const row = registryRow(page, 'Default Test Registry');
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: 'Set Default' }).click();
    await page.waitForLoadState('networkidle');

    // Should now show "Default" badge
    await expect(row.getByText('Default', { exact: true })).toBeVisible();

    // "Set Default" button should no longer be visible for this registry
    await expect(row.getByRole('button', { name: 'Set Default' })).not.toBeVisible();
  });

  test('should edit a registry', async ({ page }) => {
    // First create a registry to edit
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await nameInput.clear();
    await nameInput.fill('Registry To Edit');
    await page.getByRole('button', { name: 'Create Registry' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Click Edit on the registry
    const row = registryRow(page, 'Registry To Edit');
    await row.getByRole('link', { name: 'Edit' }).click();
    await page.waitForLoadState('networkidle');

    // Should be on edit page
    await expect(page.getByRole('heading', { name: 'Edit Registry' })).toBeVisible();

    // Change the name
    const editNameInput = page.locator('input[placeholder="Docker Hub"]');
    await editNameInput.clear();
    await editNameInput.fill('Updated Registry Name');

    // Add credentials
    await page.fill('input[placeholder="username"]', 'edituser');
    await page.fill('input[placeholder="(unchanged)"]', 'editpass123');

    // Screenshot: Edit registry form
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'registry-05-edit-form.png'),
      fullPage: false
    });

    // Save
    await page.getByRole('button', { name: 'Save Changes' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Updated name should be visible
    await expect(registryRow(page, 'Updated Registry Name')).toBeVisible();
  });

  test('should delete a registry with confirmation', async ({ page }) => {
    // First create a registry to delete
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await nameInput.clear();
    await nameInput.fill('Registry To Delete');
    await page.getByRole('button', { name: 'Create Registry' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Click Delete on the registry
    const row = registryRow(page, 'Registry To Delete');
    await row.getByRole('link', { name: 'Delete' }).click();
    await page.waitForLoadState('networkidle');

    // Should show delete confirmation
    await expect(page.getByRole('heading', { name: 'Delete Registry' })).toBeVisible();
    await expect(page.getByText('This action cannot be undone')).toBeVisible();
    await expect(page.getByText('Registry To Delete')).toBeVisible();

    // Screenshot: Delete confirmation page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'registry-06-delete-confirm.png'),
      fullPage: false
    });

    // Confirm delete
    await page.getByRole('button', { name: 'Delete Registry' }).click();

    // Should show success message
    await expect(page.getByText('Registry Deleted')).toBeVisible({ timeout: 5000 });

    // Should redirect back to list
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Deleted registry should not appear in the list
    await expect(page.locator('h5').filter({ hasText: 'Registry To Delete' })).not.toBeVisible();
  });

  test('should require name and URL to create registry', async ({ page }) => {
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    // Select Custom to clear the pre-filled URL
    const registrySelect = page.locator('select');
    await registrySelect.selectOption('');

    // Name and URL should be empty now
    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await expect(nameInput).toHaveValue('');

    // Try to submit - HTML5 validation should prevent
    await page.getByRole('button', { name: 'Create Registry' }).click();

    // Should still be on add page (form didn't submit)
    await expect(page.getByRole('heading', { name: 'Add Container Registry' })).toBeVisible();
  });

  test('should cancel from add registry page', async ({ page }) => {
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    // Click Cancel
    await page.getByRole('link', { name: 'Cancel' }).click();

    // Should navigate back to registries list
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await expect(page.getByRole('heading', { name: 'Container Registries' })).toBeVisible();
  });

  test('should cancel from delete confirmation page', async ({ page }) => {
    // First create a registry
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await nameInput.clear();
    await nameInput.fill('Cancel Delete Registry');
    await page.getByRole('button', { name: 'Create Registry' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Click Delete
    const row = registryRow(page, 'Cancel Delete Registry');
    await row.getByRole('link', { name: 'Delete' }).click();
    await page.waitForLoadState('networkidle');

    // Confirm we're on delete page
    await expect(page.getByRole('heading', { name: 'Delete Registry' })).toBeVisible();

    // Click Cancel
    await page.getByRole('link', { name: 'Cancel' }).click();

    // Should navigate back to registries list
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });

    // Registry should still exist
    await expect(registryRow(page, 'Cancel Delete Registry')).toBeVisible();
  });

  test('should show image patterns in registry list', async ({ page }) => {
    // Create a registry with patterns
    await page.goto('/settings/registries/add');
    await page.waitForLoadState('networkidle');

    const nameInput = page.locator('input[placeholder="Docker Hub"]');
    await nameInput.clear();
    await nameInput.fill('Pattern Test Registry');

    // Add patterns
    await page.fill('textarea', 'testpattern/*\ntestpattern/**');

    await page.getByRole('button', { name: 'Create Registry' }).click();
    await page.waitForURL(/\/settings\/registries$/, { timeout: 5000 });
    await page.waitForLoadState('networkidle');

    // Patterns should be visible in the row
    const row = registryRow(page, 'Pattern Test Registry');
    await expect(row.getByText('testpattern/*', { exact: true })).toBeVisible();
    await expect(row.getByText('testpattern/**', { exact: true })).toBeVisible();
  });
});
