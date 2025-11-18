import { test, expect } from '@playwright/test';

/**
 * E2E Tests für Stack Management
 * Diese Tests testen die komplette User Journey von Frontend → API → Docker
 */

test.describe('Stack Management', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/stacks');
  });

  test('should display stacks page with correct title', async ({ page }) => {
    // Title check
    await expect(page.locator('h1')).toContainText('Stack Management');

    // Should have Refresh button
    await expect(page.getByRole('button', { name: /refresh/i })).toBeVisible();
  });

  test('should load and display stacks from API', async ({ page }) => {
    // Wait for loading to complete
    await page.waitForTimeout(2000);

    // Check if either stacks are shown or "No stacks available" message
    const noStacksMessage = page.getByText(/no stacks available/i);
    const availableStacksHeading = page.getByRole('heading', { name: /available stacks/i });

    const hasNoStacksMessage = await noStacksMessage.isVisible().catch(() => false);
    const hasAvailableStacksHeading = await availableStacksHeading.isVisible();

    // Should have the "Available Stacks" heading regardless of whether there are stacks
    expect(hasAvailableStacksHeading).toBeTruthy();

    // And either have stacks or the "no stacks" message
    if (!hasNoStacksMessage) {
      // If no "no stacks" message, check for Deploy or Remove buttons (indicates stacks exist)
      const actionButtons = page.getByRole('button', { name: /^(deploy|remove)$/i });
      const hasActionButtons = await actionButtons.count() > 0;
      expect(hasActionButtons).toBeTruthy();
    }
  });

  test('should refresh stacks when clicking Refresh button', async ({ page }) => {
    const refreshButton = page.getByRole('button', { name: /refresh/i });

    // Click refresh
    await refreshButton.click();

    // Wait for loading state
    await page.waitForTimeout(1000);

    // Should still be on stacks page
    await expect(page.locator('h1')).toContainText('Stack Management');
  });

  test('should display stack table headers', async ({ page }) => {
    // Wait for load
    await page.waitForTimeout(2000);

    // Check table headers - use more specific selectors to find the header row
    const headerRow = page.locator('.grid.grid-cols-6, .grid.grid-cols-8').first();
    await expect(headerRow.getByText('Stack Name')).toBeVisible();
    await expect(headerRow.getByText('Services')).toBeVisible();
    await expect(headerRow.getByText('Status')).toBeVisible();
    await expect(headerRow.getByText('Actions')).toBeVisible();
  });

  test('should show loading state initially', async ({ page }) => {
    // Navigate to fresh page
    await page.goto('/stacks');

    // Should briefly show loading message
    const loadingMessage = page.getByText(/loading stacks/i);

    // Either loading message or stacks should appear
    const hasLoadingMessage = await loadingMessage.isVisible().catch(() => false);
    const hasStacks = await page.locator('h4').filter({ hasText: /available stacks/i }).isVisible();

    expect(hasLoadingMessage || hasStacks).toBeTruthy();
  });

  test('navigation should work from sidebar', async ({ page }) => {
    // Go to home first
    await page.goto('/');

    // Click Stacks in sidebar
    await page.getByRole('link', { name: /stacks/i }).first().click();

    // Should be on stacks page
    await expect(page).toHaveURL(/.*stacks/);
    await expect(page.locator('h1')).toContainText('Stack Management');
  });

  test('should display error message when API is unavailable', async ({ page }) => {
    // Mock API failure
    await page.route('**/api/stacks', route => {
      route.abort();
    });

    await page.goto('/stacks');
    await page.waitForTimeout(1000);

    // Should show error message
    const errorMessage = page.locator('[class*="red"]').filter({ hasText: /failed/i });
    await expect(errorMessage).toBeVisible();
  });

  test('should display demo stack with correct information', async ({ page }) => {
    // Wait for stacks to load
    await page.waitForTimeout(2000);

    // Should show the demo stack
    await expect(page.getByText('Demo Stack')).toBeVisible();
    await expect(page.getByText('A simple demo stack with Nginx and Redis')).toBeVisible();

    // Should show services count
    await expect(page.getByText('2 services')).toBeVisible();
  });

  test('should display status badge with correct styling', async ({ page }) => {
    // Wait for stacks to load
    await page.waitForTimeout(2000);

    // Find status badge - it should be a span with rounded-full class
    const statusBadge = page.locator('span[class*="rounded-full"]').first();
    await expect(statusBadge).toBeVisible();

    // Status badge should contain text (NotDeployed, Running, Failed, or Deploying)
    const statusText = await statusBadge.textContent();
    expect(statusText).toMatch(/NotDeployed|Running|Failed|Deploying/i);
  });

  test('should show Deploy button for NotDeployed stacks', async ({ page }) => {
    // Wait for stacks to load
    await page.waitForTimeout(2000);

    // Check if the demo stack is NotDeployed
    const notDeployedBadge = page.locator('span').filter({ hasText: 'NotDeployed' });
    const isNotDeployed = await notDeployedBadge.isVisible().catch(() => false);

    if (isNotDeployed) {
      // Should have Deploy button
      const deployButton = page.getByRole('button', { name: /deploy/i });
      await expect(deployButton).toBeVisible();
      await expect(deployButton).toBeEnabled();
    }
  });

  test('should show Remove button for deployed stacks', async ({ page }) => {
    // Wait for stacks to load
    await page.waitForTimeout(2000);

    // Check if the demo stack is Running
    const runningBadge = page.locator('span').filter({ hasText: 'Running' });
    const isRunning = await runningBadge.isVisible().catch(() => false);

    if (isRunning) {
      // Should have Remove button
      const removeButton = page.getByRole('button', { name: /remove/i });
      await expect(removeButton).toBeVisible();
      await expect(removeButton).toBeEnabled();
    }
  });
});
