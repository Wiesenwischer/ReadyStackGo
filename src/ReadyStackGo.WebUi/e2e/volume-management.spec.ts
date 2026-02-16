import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Docker Volumes Management
 * Tests the full volume lifecycle and captures screenshots for documentation.
 */

test.describe('Volume Management', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should navigate to volumes from sidebar', async ({ page }) => {
    // Click volumes in sidebar
    await page.click('a[href="/volumes"]');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Volume Management' })).toBeVisible();

    // Screenshot: Volume list page (may have existing volumes from Docker)
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'volumes-01-list.png'),
      fullPage: false
    });
  });

  test('should show create volume form', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // Click Create Volume button
    await page.getByRole('button', { name: 'Create Volume' }).click();

    // Form should appear
    await expect(page.getByRole('heading', { name: 'Create New Volume' })).toBeVisible();
    await expect(page.locator('input[placeholder="my-volume"]')).toBeVisible();
    await expect(page.locator('input[placeholder="local"]')).toBeVisible();

    // Screenshot: Create form visible
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'volumes-02-create-form.png'),
      fullPage: false
    });
  });

  test('should create a volume', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // Open create form
    await page.getByRole('button', { name: 'Create Volume' }).click();
    await expect(page.getByRole('heading', { name: 'Create New Volume' })).toBeVisible();

    // Fill in name
    await page.fill('input[placeholder="my-volume"]', 'rsgo-e2e-test-volume');

    // Submit
    await page.getByRole('button', { name: 'Create', exact: true }).click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Volume should appear in the list
    await expect(page.getByText('rsgo-e2e-test-volume')).toBeVisible();

    // Screenshot: Volume created and visible in list
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'volumes-03-volume-created.png'),
      fullPage: false
    });
  });

  test('should navigate to volume detail page', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // Click on the volume name link
    const volumeLink = page.getByRole('link', { name: 'rsgo-e2e-test-volume' });
    await expect(volumeLink).toBeVisible();
    await volumeLink.click();
    await page.waitForLoadState('networkidle');

    // Should show detail page with volume info
    await expect(page.getByRole('heading', { name: 'rsgo-e2e-test-volume' })).toBeVisible();
    await expect(page.getByText('Volume Information')).toBeVisible();
    await expect(page.getByText('Referenced by Containers')).toBeVisible();

    // Screenshot: Volume detail page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'volumes-04-detail.png'),
      fullPage: false
    });
  });

  test('should show orphaned badge for unused volume', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // The test volume we created should be orphaned (no container uses it)
    const volumeRow = page.locator('div.grid', { hasText: 'rsgo-e2e-test-volume' }).last();
    await expect(volumeRow.getByText('orphaned')).toBeVisible();
    await expect(volumeRow.getByText('0', { exact: true })).toBeVisible(); // 0 containers

    // Screenshot: Orphaned volume badge visible
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'volumes-05-orphaned-badge.png'),
      fullPage: false
    });
  });

  test('should toggle orphaned-only filter', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // Check "Orphaned only" checkbox (if visible — requires at least one orphaned volume)
    const orphanedCheckbox = page.getByText(/Orphaned only/);
    const isVisible = await orphanedCheckbox.isVisible().catch(() => false);

    if (isVisible) {
      await orphanedCheckbox.click();
      await page.waitForTimeout(500);

      // Screenshot: Orphaned filter active
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'volumes-06-orphaned-filter.png'),
        fullPage: false
      });
    }
  });

  test('should show delete confirmation for volume', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // Find our test volume row and click Remove
    const volumeRow = page.locator('div.grid', { hasText: 'rsgo-e2e-test-volume' }).last();
    await volumeRow.getByRole('button', { name: 'Remove' }).click();

    // Confirm/Cancel buttons should appear
    await expect(volumeRow.getByRole('button', { name: 'Confirm' })).toBeVisible();
    await expect(volumeRow.getByRole('button', { name: 'Cancel' })).toBeVisible();

    // Screenshot: Delete confirmation inline
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'volumes-07-delete-confirm.png'),
      fullPage: false
    });

    // Cancel the delete
    await volumeRow.getByRole('button', { name: 'Cancel' }).click();
    await expect(volumeRow.getByRole('button', { name: 'Remove' })).toBeVisible();
  });

  test('should delete a volume', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    // Find our test volume row and click Remove
    const volumeRow = page.locator('div.grid', { hasText: 'rsgo-e2e-test-volume' }).last();
    await volumeRow.getByRole('button', { name: 'Remove' }).click();

    // Confirm delete
    await volumeRow.getByRole('button', { name: 'Confirm' }).click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Volume should be gone
    await expect(page.getByText('rsgo-e2e-test-volume')).not.toBeVisible();
  });

  test('should cancel create form', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Create Volume' }).click();
    await expect(page.getByRole('heading', { name: 'Create New Volume' })).toBeVisible();

    // Fill in name then cancel
    await page.fill('input[placeholder="my-volume"]', 'cancelled-volume');
    await page.getByRole('button', { name: 'Cancel' }).click();

    // Form should be hidden
    await expect(page.getByRole('heading', { name: 'Create New Volume' })).not.toBeVisible();
  });

  test('should disable create button when name is empty', async ({ page }) => {
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Create Volume' }).click();

    // Create button should be disabled when name is empty
    const createButton = page.getByRole('button', { name: 'Create', exact: true });
    await expect(createButton).toBeDisabled();

    // Form should still be visible
    await expect(page.getByRole('heading', { name: 'Create New Volume' })).toBeVisible();
  });

  test('should delete volume from detail page', async ({ page }) => {
    // First create a volume to delete
    await page.goto('/volumes');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Create Volume' }).click();
    await page.fill('input[placeholder="my-volume"]', 'rsgo-e2e-delete-from-detail');
    await page.getByRole('button', { name: 'Create', exact: true }).click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Navigate to detail
    await page.getByRole('link', { name: 'rsgo-e2e-delete-from-detail' }).click();
    await page.waitForLoadState('networkidle');

    // Click Remove Volume
    await page.getByRole('button', { name: 'Remove Volume' }).click();

    // Confirm should appear
    await expect(page.getByRole('button', { name: 'Confirm Remove' })).toBeVisible();

    // Confirm delete
    await page.getByRole('button', { name: 'Confirm Remove' }).click();

    // Should redirect back to volumes list
    await page.waitForURL('/volumes', { timeout: 5000 });
  });
});
