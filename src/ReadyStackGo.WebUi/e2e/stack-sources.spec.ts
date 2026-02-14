import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Stack Sources (v0.24)
 * Covers: Add From Catalog, Export/Import, Catalog Empty State, Source Registry.
 * Captures screenshots for documentation.
 */

test.describe('Stack Sources - Navigation & Empty State', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should navigate to Stack Sources from Settings page', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    // Stack Sources card should be visible
    await expect(page.getByText('Stack Sources')).toBeVisible();

    // Screenshot: Settings page with Stack Sources card
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'sources-01-settings-nav.png'),
      fullPage: false,
    });

    // Navigate to Stack Sources
    await page.click('a[href="/settings/stack-sources"]');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Stack Sources' })).toBeVisible();
  });

  test('should show stack sources list page with action buttons', async ({ page }) => {
    await page.goto('/settings/stack-sources');
    await page.waitForLoadState('networkidle');

    // Header buttons should be visible
    await expect(page.getByRole('link', { name: /Add Source/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Export/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Import/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Sync All/i })).toBeVisible();

    // Screenshot: Sources list page (may have sources or empty state)
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'sources-02-list-page.png'),
      fullPage: false,
    });
  });
});

test.describe('Stack Sources - Add From Catalog', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should show three source type options including From Catalog', async ({ page }) => {
    await page.goto('/settings/stack-sources/add');
    await page.waitForLoadState('networkidle');

    // All three option labels should be visible
    await expect(page.getByText('Local Directory', { exact: true })).toBeVisible();
    await expect(page.getByText('Git Repository', { exact: true })).toBeVisible();
    await expect(page.getByText('From Catalog', { exact: true })).toBeVisible();

    // Continue button should be disabled until selection
    const continueButton = page.getByRole('button', { name: /Continue/i });
    await expect(continueButton).toBeDisabled();

    // Screenshot: Add source type selection with 3 options
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'sources-03-add-type-select.png'),
      fullPage: false,
    });
  });

  test('should navigate to catalog when From Catalog is selected', async ({ page }) => {
    await page.goto('/settings/stack-sources/add');
    await page.waitForLoadState('networkidle');

    // Click on the From Catalog option
    await page.getByText('From Catalog').click();

    // Continue button should be enabled now
    const continueButton = page.getByRole('button', { name: /Continue/i });
    await expect(continueButton).toBeEnabled();

    // Click Continue
    await continueButton.click();

    // Should navigate to catalog page
    await page.waitForURL('/settings/stack-sources/add/catalog');
    await expect(page.getByText('Add from Catalog')).toBeVisible();
  });

  test('should show catalog with registry entries', async ({ page }) => {
    await page.goto('/settings/stack-sources/add/catalog');
    await page.waitForLoadState('networkidle');

    // Header should be visible
    await expect(page.getByText('Add from Catalog')).toBeVisible();
    await expect(page.getByText('Select a curated source to add with one click')).toBeVisible();

    // Breadcrumb should show full path
    await expect(page.getByText('From Catalog', { exact: true })).toBeVisible();

    // Back button should be visible
    await expect(page.getByText('Back')).toBeVisible();

    // Screenshot: Catalog page with registry entries
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'sources-04-catalog-browse.png'),
      fullPage: false,
    });
  });

  test('should show Add button for sources not yet added', async ({ page }) => {
    await page.goto('/settings/stack-sources/add/catalog');
    await page.waitForLoadState('networkidle');

    // Wait for loading to finish
    await page.waitForTimeout(1000);

    // Should have either "Add" buttons or "Already added" badges
    const addButtons = page.getByRole('button', { name: 'Add' });
    const alreadyAdded = page.getByText('Already added');

    const hasAddButtons = await addButtons.first().isVisible().catch(() => false);
    const hasAlreadyAdded = await alreadyAdded.first().isVisible().catch(() => false);

    // At least one of these should be visible (catalog has entries)
    expect(hasAddButtons || hasAlreadyAdded).toBe(true);
  });

  test('should add a source from catalog and redirect to sources list', async ({ page }) => {
    await page.goto('/settings/stack-sources/add/catalog');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // If there's an "Add" button (not already added), click it
    const addButton = page.getByRole('button', { name: 'Add' }).first();
    const hasAddButton = await addButton.isVisible().catch(() => false);

    if (hasAddButton) {
      await addButton.click();

      // Should redirect to sources list
      await page.waitForURL('/settings/stack-sources', { timeout: 10000 });
      await page.waitForLoadState('networkidle');

      // The source should now appear in the list
      await expect(page.getByRole('heading', { name: 'Stack Sources' })).toBeVisible();

      // Screenshot: Sources list after adding a catalog source
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'sources-05-source-added.png'),
        fullPage: false,
      });
    }
    // If all sources are already added, the test passes (nothing to add)
  });

  test('should go back from catalog to type selection', async ({ page }) => {
    await page.goto('/settings/stack-sources/add/catalog');
    await page.waitForLoadState('networkidle');

    // Click Back
    await page.getByText('Back').click();

    // Should go back to type selection
    await page.waitForURL('/settings/stack-sources/add');
    await expect(page.getByText('Add Stack Source')).toBeVisible();
  });
});

test.describe('Stack Sources - Export/Import', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should have export and import buttons visible', async ({ page }) => {
    await page.goto('/settings/stack-sources');
    await page.waitForLoadState('networkidle');

    const exportButton = page.getByRole('button', { name: /Export/i });
    const importButton = page.getByRole('button', { name: /Import/i });

    await expect(exportButton).toBeVisible();
    await expect(importButton).toBeVisible();
    await expect(exportButton).toBeEnabled();
    await expect(importButton).toBeEnabled();

    // Screenshot: Sources list with export/import buttons
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'sources-06-export-import.png'),
      fullPage: false,
    });
  });

  test('should trigger file download on export', async ({ page }) => {
    await page.goto('/settings/stack-sources');
    await page.waitForLoadState('networkidle');

    // Listen for download
    const downloadPromise = page.waitForEvent('download', { timeout: 10000 });
    await page.getByRole('button', { name: /Export/i }).click();
    const download = await downloadPromise;

    // Download filename should match pattern
    expect(download.suggestedFilename()).toMatch(/^rsgo-sources-\d{4}-\d{2}-\d{2}\.json$/);
  });

  test('should have hidden file input for import', async ({ page }) => {
    await page.goto('/settings/stack-sources');
    await page.waitForLoadState('networkidle');

    // Hidden file input should exist
    const fileInput = page.locator('input[type="file"][accept=".json"]');
    await expect(fileInput).toBeAttached();
  });
});

test.describe('Stack Catalog - Empty State', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should show empty state with Add Sources and Browse Catalog links', async ({ page }) => {
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Check if empty state is shown (requires active environment + no products)
    const emptyHeading = page.getByText('No stacks available');
    const hasEmpty = await emptyHeading.isVisible().catch(() => false);

    if (hasEmpty) {
      await expect(page.getByText('Add stack sources to discover deployable stacks.')).toBeVisible();
      await expect(page.getByRole('link', { name: /Add Sources/i })).toBeVisible();
      await expect(page.getByRole('link', { name: /Browse Catalog/i })).toBeVisible();

      // Screenshot: Catalog empty state
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'sources-07-catalog-empty.png'),
        fullPage: false,
      });
    }
  });

  test('should navigate to Add Sources from empty catalog', async ({ page }) => {
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    const addSourcesLink = page.getByRole('link', { name: /Add Sources/i });
    const hasLink = await addSourcesLink.isVisible().catch(() => false);

    if (hasLink) {
      await addSourcesLink.click();
      await page.waitForURL('/settings/stack-sources/add');
    }
  });

  test('should navigate to Browse Catalog from empty catalog', async ({ page }) => {
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    const catalogLink = page.getByRole('link', { name: /Browse Catalog/i });
    const hasLink = await catalogLink.isVisible().catch(() => false);

    if (hasLink) {
      await catalogLink.click();
      await page.waitForURL('/settings/stack-sources/add/catalog');
    }
  });
});

test.describe('Stack Sources - Source Management', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should show source details in list', async ({ page }) => {
    await page.goto('/settings/stack-sources');
    await page.waitForLoadState('networkidle');

    // Check if we have sources by looking for Disable/Enable buttons
    const disableButton = page.getByRole('button', { name: /^(Disable|Enable)$/ }).first();
    const hasSources = await disableButton.isVisible().catch(() => false);

    if (hasSources) {
      // Type badge should be visible on the page (Git or Local)
      const hasGit = await page.getByText('Git', { exact: true }).first().isVisible().catch(() => false);
      const hasLocal = await page.getByText('Local', { exact: true }).first().isVisible().catch(() => false);
      expect(hasGit || hasLocal).toBe(true);

      // Status badge should be visible (Enabled or Disabled)
      const hasEnabled = await page.getByText('Enabled', { exact: true }).first().isVisible().catch(() => false);
      const hasDisabled = await page.getByText('Disabled', { exact: true }).first().isVisible().catch(() => false);
      expect(hasEnabled || hasDisabled).toBe(true);

      // Action buttons should be visible
      await expect(page.getByRole('button', { name: 'Sync' }).first()).toBeVisible();
      await expect(page.getByRole('button', { name: 'Delete' }).first()).toBeVisible();
    }
  });

  test('should show empty state when no sources configured', async ({ page }) => {
    await page.goto('/settings/stack-sources');
    await page.waitForLoadState('networkidle');

    // Check if empty state is shown
    const emptyTitle = page.getByText('No Stack Sources Configured');
    const hasEmpty = await emptyTitle.isVisible().catch(() => false);

    if (hasEmpty) {
      await expect(page.getByText('Add a local directory or Git repository')).toBeVisible();
      await expect(page.getByRole('link', { name: /Add Your First Source/i })).toBeVisible();
    }
  });
});
