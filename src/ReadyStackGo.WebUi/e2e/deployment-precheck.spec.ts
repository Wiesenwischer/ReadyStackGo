import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { login } from './helpers/auth';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Deployment Precheck
 * Tests the precheck workflow during stack deployment and captures screenshots for documentation.
 *
 * Requires: At least one stack source with products in the catalog (e.g. RSGO Embedded Stacks).
 */

/** Navigate from catalog to the deploy page of a stack and wait for precheck */
async function navigateToDeployPage(page: import('@playwright/test').Page) {
  // Go to catalog and find a product
  await page.goto('/catalog');
  await page.waitForLoadState('networkidle');

  // Click "View Details" on a product to open product detail
  const viewDetailsLink = page.getByRole('link', { name: 'View Details' });
  await expect(viewDetailsLink.first()).toBeVisible({ timeout: 10000 });
  await viewDetailsLink.last().click();
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);

  // Find the deploy link and extract its href for direct navigation
  // (client-side Link navigation can be unreliable in E2E)
  const deployHref = await page.locator('a[href^="/deploy/"]').first().getAttribute('href');
  expect(deployHref).toBeTruthy();

  // Navigate directly to the deploy page
  await page.goto(deployHref!);
  await page.waitForLoadState('networkidle');
}

test.describe('Deployment Precheck', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should navigate from catalog to deploy page and show precheck', async ({ page }) => {
    // Navigate to catalog
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    // Screenshot: Catalog page with available products
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-01-catalog.png'),
      fullPage: false,
    });

    // Click View Details on a product
    const viewDetailsLink = page.getByRole('link', { name: 'View Details' });
    await expect(viewDetailsLink.first()).toBeVisible({ timeout: 10000 });
    await viewDetailsLink.last().click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Get the deploy link href and navigate directly
    const deployHref = await page.locator('a[href^="/deploy/"]').first().getAttribute('href');
    expect(deployHref).toBeTruthy();
    await page.goto(deployHref!);
    await page.waitForLoadState('networkidle');

    // Should show Stack Configuration
    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });

    // Screenshot: Deploy Stack configuration page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-02-configure.png'),
      fullPage: false,
    });

    // The precheck should auto-run when entering configure state
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    const precheckLoading = page.getByText('Running deployment precheck...');

    await expect(precheckHeading.or(precheckLoading)).toBeVisible({ timeout: 15000 });

    // If still loading, wait for completion
    if (await precheckLoading.isVisible().catch(() => false)) {
      await expect(precheckHeading).toBeVisible({ timeout: 30000 });
    }

    // Scroll precheck panel into view for screenshot
    await precheckHeading.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);

    // Screenshot: Precheck panel with results
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-03-results.png'),
      fullPage: false,
    });
  });

  test('should show check items with severity icons', async ({ page }) => {
    await navigateToDeployPage(page);

    // Wait for precheck to complete
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(1000);

    // Verify check items are displayed with severity-colored backgrounds
    const checkItems = page.locator('[class*="bg-green-50"], [class*="bg-yellow-50"], [class*="bg-red-50"]');
    const itemCount = await checkItems.count();
    expect(itemCount).toBeGreaterThan(0);
  });

  test('should allow re-check via button', async ({ page }) => {
    await navigateToDeployPage(page);

    // Wait for precheck to complete
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });

    // Find and click the Re-Check button
    const recheckButton = page.getByText('Re-Check');
    await expect(recheckButton).toBeVisible();

    // Scroll precheck panel into view
    await precheckHeading.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);

    // Screenshot: Precheck panel with Re-Check button
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-04-recheck-button.png'),
      fullPage: false,
    });

    await recheckButton.click();

    // Wait for re-check to complete
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(2000);

    // Check items should still be present after re-check
    const checkItems = page.locator('[class*="bg-green-50"], [class*="bg-yellow-50"], [class*="bg-red-50"]');
    const itemCount = await checkItems.count();
    expect(itemCount).toBeGreaterThan(0);
  });

  test('should control deploy button based on precheck result', async ({ page }) => {
    await navigateToDeployPage(page);

    // Wait for precheck to complete
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });

    // Check the deploy button state
    const deploySubmitButton = page.getByRole('button', { name: /Deploy to/i });
    await expect(deploySubmitButton).toBeVisible();

    // If precheck has errors (red summary), deploy button should be disabled
    const hasErrors = await page.locator('[class*="bg-red-50"][class*="border-red"]').first().isVisible().catch(() => false);

    if (hasErrors) {
      await expect(deploySubmitButton).toBeDisabled();
    } else {
      await expect(deploySubmitButton).toBeEnabled();
    }

    // Scroll deploy button into view
    await deploySubmitButton.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);

    // Screenshot: Deploy button state
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-05-deploy-button.png'),
      fullPage: false,
    });
  });

  test('should show precheck loading state on re-check', async ({ page }) => {
    await navigateToDeployPage(page);

    // Wait for initial precheck to complete
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });

    // Click re-check and capture loading state
    const recheckButton = page.getByText('Re-Check');
    await recheckButton.click();

    // Brief wait to capture the spinner
    await page.waitForTimeout(200);

    // Scroll precheck panel into view
    await precheckHeading.scrollIntoViewIfNeeded();

    // Screenshot: Loading/checking state with spinner
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-06-loading.png'),
      fullPage: false,
    });

    // Wait for completion
    await page.waitForTimeout(5000);
    await expect(precheckHeading).toBeVisible();
  });

  test('should not show precheck for custom deploy', async ({ page }) => {
    await page.goto('/deploy/custom');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Precheck should NOT be visible for custom deployments
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    const precheckLoading = page.getByText('Running deployment precheck...');

    await expect(precheckHeading).not.toBeVisible();
    await expect(precheckLoading).not.toBeVisible();
  });
});
