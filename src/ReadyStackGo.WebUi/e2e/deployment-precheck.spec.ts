import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { login } from './helpers/auth';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Deployment Precheck
 * Tests the precheck workflow: button in sidebar → separate precheck page with results.
 *
 * Requires: At least one stack source with products in the catalog (e.g. RSGO Embedded Stacks).
 */

/** Navigate from catalog to the deploy page of a stack */
async function navigateToDeployPage(page: import('@playwright/test').Page) {
  await page.goto('/catalog');
  await page.waitForLoadState('networkidle');

  const viewDetailsLink = page.getByRole('link', { name: 'View Details' });
  await expect(viewDetailsLink.first()).toBeVisible({ timeout: 10000 });
  await viewDetailsLink.last().click();
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);

  // Find deploy link and navigate directly
  const deployHref = await page.locator('a[href^="/deploy/"]').first().getAttribute('href');
  expect(deployHref).toBeTruthy();
  await page.goto(deployHref!);
  await page.waitForLoadState('networkidle');
}

test.describe('Deployment Precheck', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should show Run Precheck button on deploy page', async ({ page }) => {
    // Navigate to catalog
    await page.goto('/catalog');
    await page.waitForLoadState('networkidle');

    // Screenshot: Catalog page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-01-catalog.png'),
      fullPage: false,
    });

    await navigateToDeployPage(page);

    // Should show Stack Configuration
    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });

    // Screenshot: Deploy page with configuration
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-02-configure.png'),
      fullPage: false,
    });

    // Precheck should NOT auto-run — no precheck panel visible
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    await expect(precheckHeading).not.toBeVisible();

    // Run Precheck button should be visible in sidebar
    const precheckButton = page.getByRole('button', { name: 'Run Precheck' });
    await expect(precheckButton).toBeVisible();

    // Scroll sidebar into view for screenshot
    await precheckButton.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);

    // Screenshot: Sidebar with Run Precheck button
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-03-run-button.png'),
      fullPage: false,
    });
  });

  test('should navigate to precheck page and show results', async ({ page }) => {
    await navigateToDeployPage(page);

    // Wait for configuration to load
    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });

    // Click Run Precheck button
    const precheckButton = page.getByRole('button', { name: 'Run Precheck' });
    await expect(precheckButton).toBeVisible();
    await precheckButton.click();

    // Should navigate to precheck page
    await page.waitForURL(/\/precheck$/, { timeout: 10000 });

    // Wait for precheck to complete
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    const loadingText = page.getByText('Running Deployment Precheck');

    // Either loading or results should be visible
    await expect(precheckHeading.or(loadingText)).toBeVisible({ timeout: 15000 });

    // Wait for results if still loading
    if (await loadingText.isVisible().catch(() => false)) {
      await expect(precheckHeading).toBeVisible({ timeout: 30000 });
    }

    // Screenshot: Precheck results page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-04-results.png'),
      fullPage: false,
    });

    // Check items should be displayed
    const checkItems = page.locator('[class*="bg-green-50"], [class*="bg-yellow-50"], [class*="bg-red-50"]');
    const itemCount = await checkItems.count();
    expect(itemCount).toBeGreaterThan(0);
  });

  test('should allow re-check on precheck page', async ({ page }) => {
    await navigateToDeployPage(page);
    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });

    // Navigate to precheck page
    await page.getByRole('button', { name: 'Run Precheck' }).click();
    await page.waitForURL(/\/precheck$/, { timeout: 10000 });

    // Wait for results
    const precheckHeading = page.getByRole('heading', { name: 'Deployment Precheck' });
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(1000);

    // Re-Check button should be visible
    const recheckButton = page.getByText('Re-Check');
    await expect(recheckButton).toBeVisible();

    // Screenshot: Precheck page with Re-Check button
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-05-recheck.png'),
      fullPage: false,
    });

    // Click Re-Check
    await recheckButton.click();
    await page.waitForTimeout(2000);

    // Results should still be present after re-check
    await expect(precheckHeading).toBeVisible({ timeout: 30000 });
  });

  test('should have back to configure link', async ({ page }) => {
    await navigateToDeployPage(page);
    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });

    // Navigate to precheck page
    await page.getByRole('button', { name: 'Run Precheck' }).click();
    await page.waitForURL(/\/precheck$/, { timeout: 10000 });

    // Wait for results
    await expect(page.getByRole('heading', { name: 'Deployment Precheck' })).toBeVisible({ timeout: 30000 });

    // Back to Configure link should be visible
    const backLink = page.getByRole('link', { name: 'Back to Configure' });
    await expect(backLink).toBeVisible();

    // Click back — should return to deploy page
    await backLink.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });
  });

  test('should allow deploy without running precheck', async ({ page }) => {
    await navigateToDeployPage(page);
    await expect(page.getByText('Stack Configuration')).toBeVisible({ timeout: 10000 });

    // Deploy button should be enabled even without precheck
    const deployButton = page.getByRole('button', { name: /Deploy to/i });
    await expect(deployButton).toBeVisible();
    await expect(deployButton).toBeEnabled();

    // Scroll deploy button into view
    await deployButton.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);

    // Screenshot: Deploy button enabled without precheck
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'precheck-06-deploy-enabled.png'),
      fullPage: false,
    });
  });

  test('should not show precheck button for custom deploy', async ({ page }) => {
    await page.goto('/deploy/custom');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Run Precheck button should NOT be visible for custom deployments
    const precheckButton = page.getByRole('button', { name: 'Run Precheck' });
    await expect(precheckButton).not.toBeVisible();
  });
});
