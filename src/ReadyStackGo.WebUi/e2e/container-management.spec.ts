import { test, expect } from '@playwright/test';

/**
 * E2E Tests für Container Management
 * Diese Tests testen die komplette User Journey von Frontend → API → Docker
 */

test.describe('Container Management', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/containers');
  });

  test('should display containers page with correct title', async ({ page }) => {
    // Title check
    await expect(page.locator('h1')).toContainText('Container Management');

    // Should have Refresh button
    await expect(page.getByRole('button', { name: /refresh/i })).toBeVisible();
  });

  test('should load and display containers from Docker', async ({ page }) => {
    // Wait for loading to complete
    await page.waitForTimeout(2000);

    // Check if either containers are shown or "No containers found" message
    const noContainersMessage = page.getByText(/no containers found/i);
    const allContainersHeading = page.getByRole('heading', { name: /all containers/i });

    const hasNoContainersMessage = await noContainersMessage.isVisible().catch(() => false);
    const hasAllContainersHeading = await allContainersHeading.isVisible();

    // Should have the "All Containers" heading regardless of whether there are containers
    expect(hasAllContainersHeading).toBeTruthy();

    // And either have containers or the "no containers" message
    if (!hasNoContainersMessage) {
      // If no "no containers" message, check for Start or Stop buttons (indicates containers exist)
      const actionButtons = page.getByRole('button', { name: /^(start|stop)$/i });
      const hasActionButtons = await actionButtons.count() > 0;
      expect(hasActionButtons).toBeTruthy();
    }
  });

  test('should refresh containers when clicking Refresh button', async ({ page }) => {
    const refreshButton = page.getByRole('button', { name: /refresh/i });

    // Click refresh
    await refreshButton.click();

    // Wait for loading state
    await page.waitForTimeout(1000);

    // Should still be on containers page
    await expect(page.locator('h1')).toContainText('Container Management');
  });

  test('should display container properties when containers exist', async ({ page }) => {
    // Wait for load
    await page.waitForTimeout(2000);

    // Check table headers - use more specific selectors to find the header row
    const headerRow = page.locator('.grid.grid-cols-6').first();
    await expect(headerRow.getByText('Container Name')).toBeVisible();
    await expect(headerRow.getByText('Status')).toBeVisible();
    await expect(headerRow.getByText('Port')).toBeVisible();
    await expect(headerRow.getByText('Actions')).toBeVisible();
  });

  test('should show loading state initially', async ({ page }) => {
    // Navigate to fresh page
    await page.goto('/containers');

    // Should briefly show loading message
    const loadingMessage = page.getByText(/loading containers/i);

    // Either loading message or containers should appear
    const hasLoadingMessage = await loadingMessage.isVisible().catch(() => false);
    const hasContainers = await page.locator('h4').filter({ hasText: /all containers/i }).isVisible();

    expect(hasLoadingMessage || hasContainers).toBeTruthy();
  });

  test('navigation should work from sidebar', async ({ page }) => {
    // Go to home first
    await page.goto('/');

    // Click Containers in sidebar
    await page.getByRole('link', { name: /containers/i }).first().click();

    // Should be on containers page
    await expect(page).toHaveURL(/.*containers/);
    await expect(page.locator('h1')).toContainText('Container Management');
  });

  test('should display error message when API is unavailable', async ({ page }) => {
    // Mock API failure
    await page.route('**/api/containers', route => {
      route.abort();
    });

    await page.goto('/containers');
    await page.waitForTimeout(1000);

    // Should show error message
    const errorMessage = page.locator('[class*="red"]').filter({ hasText: /failed/i });
    await expect(errorMessage).toBeVisible();
  });
});
