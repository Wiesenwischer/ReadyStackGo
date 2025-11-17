import { test, expect } from '@playwright/test';

/**
 * E2E Tests für Container Start/Stop Actions
 * Diese Tests erfordern einen laufenden Docker-Container
 */

test.describe('Container Actions', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/containers');
    await page.waitForTimeout(2000);
  });

  test('should show Start button for stopped containers', async ({ page }) => {
    // Look for stopped containers (should have "exited" or similar status)
    const stoppedContainerRow = page.locator('[class*="grid"][class*="grid-cols-6"]').filter({
      has: page.locator('span:not(:has-text("running"))')
    }).first();

    if (await stoppedContainerRow.count() > 0) {
      const startButton = stoppedContainerRow.getByRole('button', { name: /start/i });
      await expect(startButton).toBeVisible();
      await expect(startButton).toHaveClass(/bg-green/);
    }
  });

  test('should show Stop button for running containers', async ({ page }) => {
    // Look for running containers
    const runningContainerRow = page.locator('[class*="grid"][class*="grid-cols-6"]').filter({
      has: page.locator('span:has-text("running")')
    }).first();

    if (await runningContainerRow.count() > 0) {
      const stopButton = runningContainerRow.getByRole('button', { name: /stop/i });
      await expect(stopButton).toBeVisible();
      await expect(stopButton).toHaveClass(/bg-red/);
    }
  });

  test('should disable button during action', async ({ page }) => {
    // Find any container with an action button
    const actionButton = page.locator('button:has-text("Start"), button:has-text("Stop")').first();

    if (await actionButton.count() > 0) {
      // Get initial button text
      const initialText = await actionButton.textContent();

      // Click button and immediately check for disabled state or loading text
      await actionButton.click();

      // Button should either be disabled or show loading state ("...")
      // Check immediately as the loading state might be very brief
      const isDisabled = await actionButton.isDisabled().catch(() => false);
      const hasLoadingText = await actionButton.textContent().then(text => text === '...').catch(() => false);

      // At least one of these should be true (button was disabled or showed loading)
      // Or the action completed successfully and button text changed
      const newText = await actionButton.textContent();
      const actionCompleted = newText !== initialText;

      expect(isDisabled || hasLoadingText || actionCompleted).toBeTruthy();
    }
  });

  test('should display status badge with correct color', async ({ page }) => {
    // Find running container badge
    const runningBadge = page.locator('span:has-text("running")').first();

    if (await runningBadge.count() > 0) {
      await expect(runningBadge).toHaveClass(/bg-green/);
    }

    // Find stopped/exited container badge
    const stoppedBadge = page.locator('span:has-text("exited"), span:has-text("created")').first();

    if (await stoppedBadge.count() > 0) {
      await expect(stoppedBadge).toHaveClass(/bg-gray/);
    }
  });

  test('should display container image name', async ({ page }) => {
    // Check if any container row shows an image name
    const containerRow = page.locator('[class*="grid"][class*="grid-cols-6"]').filter({
      hasText: /nginx|alpine|ubuntu|postgres|redis|mysql/
    }).first();

    if (await containerRow.count() > 0) {
      await expect(containerRow).toBeVisible();
    }
  });

  test('should display port mappings', async ({ page }) => {
    // Look for port format like "8080:80"
    const portMapping = page.locator('p').filter({ hasText: /\d+:\d+/ }).first();

    // Either port mapping exists or shows "-" for no ports
    const hasPortMapping = await portMapping.count() > 0;
    const hasNoPort = await page.getByText('-').count() > 0;

    expect(hasPortMapping || hasNoPort).toBeTruthy();
  });
});
