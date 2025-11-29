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

  test('CRITICAL: stop container should call API and change state', async ({ page }) => {
    // First login to get auth token
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Navigate to containers page
    await page.goto('/containers');
    await page.waitForTimeout(2000);

    // Track API calls
    const apiCalls: Array<{
      url: string;
      method: string;
      status: number;
      body: string;
      requestHeaders: Record<string, string>;
    }> = [];

    page.on('response', async (response) => {
      if (response.url().includes('/api/containers')) {
        const call = {
          url: response.url(),
          method: response.request().method(),
          status: response.status(),
          body: '',
          requestHeaders: response.request().headers(),
        };
        try {
          call.body = await response.text();
        } catch {
          call.body = 'Could not read body';
        }
        apiCalls.push(call);
        console.log(`API Call: ${call.method} ${call.url} -> ${call.status}`);
        console.log(`  Body: ${call.body}`);
        console.log(`  Auth: ${call.requestHeaders['authorization']?.substring(0, 50)}...`);
      }
    });

    // Find a running container
    const runningContainerRow = page.locator('[class*="grid"][class*="grid-cols-6"]').filter({
      has: page.locator('span:has-text("running")')
    }).first();

    const hasRunningContainer = await runningContainerRow.count() > 0;
    console.log('Has running container:', hasRunningContainer);

    if (!hasRunningContainer) {
      console.log('No running containers found, skipping test');
      return;
    }

    // Get container name for later verification
    const containerName = await runningContainerRow.locator('p').first().textContent();
    console.log('Container name:', containerName);

    // Find and click the Stop button
    const stopButton = runningContainerRow.getByRole('button', { name: /stop/i });
    await expect(stopButton).toBeVisible();

    console.log('Clicking Stop button...');
    await stopButton.click();

    // Wait for the action to complete
    await page.waitForTimeout(5000);

    // Log all API calls made
    console.log('=== API Calls Summary ===');
    for (const call of apiCalls) {
      console.log(`${call.method} ${call.url}`);
      console.log(`  Status: ${call.status}`);
      console.log(`  Body: ${call.body}`);
    }

    // Find the stop API call
    const stopCall = apiCalls.find(c => c.url.includes('/stop') && c.method === 'POST');

    if (stopCall) {
      console.log('=== Stop API Call Details ===');
      console.log('URL:', stopCall.url);
      console.log('Status:', stopCall.status);
      console.log('Body:', stopCall.body);

      // Verify the API was called and returned success
      expect(stopCall.status).toBeLessThan(400);
    } else {
      console.log('WARNING: No stop API call was made!');
      // Check if there's an error message displayed
      const errorElement = page.locator('[class*="error"], [class*="red"]').first();
      if (await errorElement.isVisible().catch(() => false)) {
        const errorText = await errorElement.textContent();
        console.log('Error displayed:', errorText);
      }
    }

    // Refresh to verify container state changed
    await page.reload();
    await page.waitForTimeout(2000);

    // Check if the container is now stopped
    const containerRow = page.locator('[class*="grid"][class*="grid-cols-6"]').filter({
      hasText: containerName || ''
    }).first();

    if (await containerRow.count() > 0) {
      const statusBadge = containerRow.locator('span').first();
      const status = await statusBadge.textContent();
      console.log('Container status after stop:', status);

      // Container should not be running anymore
      expect(status?.toLowerCase()).not.toBe('running');
    }
  });

  test('CRITICAL: verify stop API response handling', async ({ page }) => {
    // First login
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Navigate to containers
    await page.goto('/containers');
    await page.waitForTimeout(2000);

    // Track console errors
    const consoleErrors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
        console.log('Console Error:', msg.text());
      }
    });

    // Track page errors
    const pageErrors: string[] = [];
    page.on('pageerror', (error) => {
      pageErrors.push(error.message);
      console.log('Page Error:', error.message);
    });

    // Find stop button
    const stopButton = page.locator('button:has-text("Stop")').first();

    if (await stopButton.count() > 0) {
      console.log('Clicking stop button...');
      await stopButton.click();
      await page.waitForTimeout(3000);

      // Check for JSON parsing errors
      const jsonErrors = consoleErrors.filter(e =>
        e.includes('JSON') || e.includes('Unexpected token') || e.includes('parse')
      );

      console.log('Console errors:', consoleErrors);
      console.log('Page errors:', pageErrors);
      console.log('JSON-related errors:', jsonErrors);

      // There should be no JSON parsing errors
      expect(jsonErrors.length).toBe(0);
    }
  });
});
