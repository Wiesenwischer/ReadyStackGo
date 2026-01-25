import { test, expect } from '@playwright/test';

/**
 * E2E Tests für Stack Management
 * Diese Tests testen die Stacks-Seite mit dem neuen Stack-Source-System
 */

test.describe('Stack Management', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Navigate to stacks page
    await page.goto('/deployments');
  });

  test('should display stacks page with correct title', async ({ page }) => {
    // Title check
    await expect(page.locator('h1')).toContainText('Deployments');

    // Should have Sync Sources button
    await expect(page.getByRole('button', { name: /sync sources/i })).toBeVisible();

    // Should have Deploy Custom button
    await expect(page.getByRole('button', { name: /deploy custom/i })).toBeVisible();
  });

  test('should have Available Stacks section', async ({ page }) => {
    // Wait for loading to complete
    await page.waitForTimeout(2000);

    // Should have the "Available Stacks" heading
    const availableStacksHeading = page.getByRole('heading', { name: /available stacks/i });
    await expect(availableStacksHeading).toBeVisible();
  });

  test('should have Deployed Stacks section', async ({ page }) => {
    // Wait for loading to complete
    await page.waitForTimeout(2000);

    // Should have the "Deployed Stacks" heading
    const deployedStacksHeading = page.getByRole('heading', { name: /deployed stacks/i });
    await expect(deployedStacksHeading).toBeVisible();
  });

  test('should sync sources when clicking Sync Sources button', async ({ page }) => {
    const syncButton = page.getByRole('button', { name: /sync sources/i });

    // Click sync
    await syncButton.click();

    // Button should show syncing state
    await expect(page.getByRole('button', { name: /syncing/i })).toBeVisible();

    // Wait for sync to complete
    await page.waitForTimeout(3000);

    // Should be back to normal state
    await expect(page.getByRole('button', { name: /sync sources/i })).toBeVisible();
  });

  test('should show loading state initially', async ({ page }) => {
    // Navigate to fresh page
    await page.goto('/deployments');

    // Should briefly show loading message or stacks
    const loadingMessage = page.getByText(/loading/i);
    const hasAvailableStacks = page.getByRole('heading', { name: /available stacks/i });

    // Wait for either loading or content
    await Promise.race([
      loadingMessage.waitFor({ state: 'visible', timeout: 1000 }).catch(() => {}),
      hasAvailableStacks.waitFor({ state: 'visible', timeout: 3000 })
    ]);

    // Should eventually show Available Stacks heading
    await expect(hasAvailableStacks).toBeVisible();
  });

  test('navigation should work from sidebar', async ({ page }) => {
    // Go to home first
    await page.goto('/');

    // Click Stacks in sidebar
    await page.getByRole('link', { name: /stacks/i }).first().click();

    // Should be on stacks page
    await expect(page).toHaveURL(/.*stacks/);
    await expect(page.locator('h1')).toContainText('Deployments');
  });

  test('should open Deploy Custom modal when clicking button', async ({ page }) => {
    // Click Deploy Custom button
    await page.getByRole('button', { name: /deploy custom/i }).click();

    // Modal should open
    await expect(page.getByText(/deploy docker compose/i)).toBeVisible();

    // Should have file upload and textarea
    await expect(page.getByText(/paste yaml content/i)).toBeVisible();
  });

  test('should display stacks when available', async ({ page }) => {
    // Wait for stacks to load
    await page.waitForTimeout(2000);

    // Check for stack cards (they have Deploy buttons)
    const deployButtons = page.locator('button').filter({ hasText: /^deploy$/i });
    const stackCount = await deployButtons.count();

    // If stacks exist, they should have names and service counts
    if (stackCount > 0) {
      // Each stack card should have a service count badge
      const serviceBadges = page.locator('span').filter({ hasText: /service/i });
      await expect(serviceBadges.first()).toBeVisible();
    } else {
      // No stacks - should show appropriate message
      const noStacksMessage = page.getByText(/no stack definitions available/i);
      await expect(noStacksMessage).toBeVisible();
    }
  });
});
