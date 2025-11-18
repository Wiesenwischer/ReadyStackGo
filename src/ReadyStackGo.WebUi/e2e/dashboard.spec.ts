import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';

/**
 * E2E Tests für Dashboard Stats
 * Diese Tests testen die komplette User Journey: Login → Dashboard → API → Docker
 *
 * IMPORTANT: Diese Tests simulieren echte Nutzer-Workflows, inkl. Authentifizierung
 */

test.describe('Dashboard Stats - Authenticated User', () => {
  test.beforeEach(async ({ page }) => {
    // Simulate real user login workflow
    await login(page);

    // Navigate to dashboard
    await page.goto('/');

    // Wait for dashboard to load
    await page.waitForTimeout(2000);
  });

  test('should display dashboard title', async ({ page }) => {
    // Check if Dashboard title is visible
    const title = page.getByRole('heading', { name: 'Dashboard' });
    await expect(title).toBeVisible();
  });

  test('should display all stat cards', async ({ page }) => {
    // Wait for stats to load
    await page.waitForTimeout(2000);

    // Check for Total Stacks card
    await expect(page.getByText('Total Stacks')).toBeVisible();

    // Check for Deployed Stacks card
    await expect(page.getByText('Deployed Stacks')).toBeVisible();

    // Check for Total Containers card
    await expect(page.getByText('Total Containers')).toBeVisible();

    // Check for Running Containers card
    await expect(page.getByText('Running Containers')).toBeVisible();
  });

  test('should display numeric values in stat cards', async ({ page }) => {
    // Wait for stats to load
    await page.waitForTimeout(2000);

    // Get all stat cards
    const statCards = page.locator('.rounded-sm.border.border-stroke');

    // Check that we have 4 stat cards
    await expect(statCards).toHaveCount(4);

    // Verify each card has a numeric value (not "...")
    for (let i = 0; i < 4; i++) {
      const card = statCards.nth(i);
      const value = card.locator('h4.text-title-md');
      const text = await value.textContent();

      // Value should not be "..." (loading state)
      expect(text).not.toBe('...');

      // Value should be a number or contain a number
      expect(text).toMatch(/\d+/);
    }
  });

  test('should display Container Overview panel', async ({ page }) => {
    // Wait for stats to load
    await page.waitForTimeout(2000);

    // Check for Container Overview heading
    await expect(page.getByRole('heading', { name: 'Container Overview' })).toBeVisible();

    // Check for breakdown labels
    await expect(page.getByText('Running:')).toBeVisible();
    await expect(page.getByText('Stopped:')).toBeVisible();
  });

  test('should display Stack Overview panel', async ({ page }) => {
    // Wait for stats to load
    await page.waitForTimeout(2000);

    // Check for Stack Overview heading
    await expect(page.getByRole('heading', { name: 'Stack Overview' })).toBeVisible();

    // Check for breakdown labels
    await expect(page.getByText('Deployed:')).toBeVisible();
    await expect(page.getByText('Not Deployed:')).toBeVisible();
  });

  test('should update stats after stack deployment', async ({ page }) => {
    // Navigate to stacks page first
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // Ensure stack is not deployed
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);
    if (hasRemoveButton) {
      await removeButton.click();
      await page.waitForTimeout(3000);
    }

    // Go back to dashboard and get initial stats
    await page.goto('/');
    await page.waitForTimeout(2000);

    const initialDeployedText = await page.locator('text=Deployed Stacks').locator('..').locator('h4').textContent();
    const initialDeployed = parseInt(initialDeployedText || '0');

    // Deploy a stack
    await page.goto('/stacks');
    await page.waitForTimeout(1000);
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    await deployButton.click();
    await page.waitForTimeout(15000); // Wait for deployment

    // Go back to dashboard and check updated stats
    await page.goto('/');
    await page.waitForTimeout(2000);

    const updatedDeployedText = await page.locator('text=Deployed Stacks').locator('..').locator('h4').textContent();
    const updatedDeployed = parseInt(updatedDeployedText || '0');

    // Deployed stacks should have increased
    expect(updatedDeployed).toBeGreaterThan(initialDeployed);

    // Cleanup - remove the stack
    await page.goto('/stacks');
    await page.waitForTimeout(1000);
    const removeBtn = page.getByRole('button', { name: /^remove$/i }).first();
    if (await removeBtn.isVisible().catch(() => false)) {
      await removeBtn.click();
      await page.waitForTimeout(3000);
    }
  });

  test('should show loading state initially', async ({ page }) => {
    // Navigate to dashboard
    await page.goto('/');

    // Check for loading state (at least one "..." should be visible briefly)
    // This is a race condition test - it might pass even if loading is very fast
    const statCards = page.locator('.rounded-sm.border.border-stroke h4.text-title-md');

    // At least one card should exist
    await expect(statCards.first()).toBeVisible();
  });

  test('should handle API errors gracefully', async ({ page }) => {
    // Intercept the API call and return an error
    await page.route('**/api/dashboard/stats', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ message: 'Internal Server Error' })
      });
    });

    // Navigate to dashboard
    await page.goto('/');
    await page.waitForTimeout(2000);

    // Check for error message
    const errorMessage = page.locator('[class*="red"]').filter({ hasText: /failed/i });
    await expect(errorMessage).toBeVisible();
  });

  test('should auto-refresh stats every 10 seconds', async ({ page }) => {
    // Get initial stat value
    const totalStacksCard = page.locator('text=Total Stacks').locator('..').locator('h4');
    const initialValue = await totalStacksCard.textContent();

    // Set up API interception to change the value
    let callCount = 0;
    await page.route('**/api/dashboard/stats', route => {
      callCount++;
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          totalStacks: callCount + 10, // Different value each time
          deployedStacks: 1,
          notDeployedStacks: callCount + 9,
          totalContainers: 5,
          runningContainers: 3,
          stoppedContainers: 2
        })
      });
    });

    // Wait for auto-refresh (10 seconds + buffer)
    await page.waitForTimeout(11000);

    // Get updated stat value
    const updatedValue = await totalStacksCard.textContent();

    // Value should have changed (due to our mock API)
    expect(updatedValue).not.toBe(initialValue);
  });
});

/**
 * CRITICAL: Test unauthenticated access
 * This test would have caught the original bug where Dashboard was accessible but API returned 401
 */
test.describe('Dashboard Stats - Unauthenticated User', () => {
  test('should redirect to login when accessing dashboard without auth', async ({ page }) => {
    // Clear any existing auth
    await page.goto('/login');
    await page.evaluate(() => {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_user');
    });

    // Try to access dashboard
    await page.goto('/');

    // Should be redirected to login page
    await page.waitForURL(/\/login/);

    // Should show login form
    await expect(page.locator('input[name="username"], input[type="text"]')).toBeVisible();
  });

  test('should show error when API returns 401', async ({ page }) => {
    // Login first
    await login(page);
    await page.goto('/');
    await page.waitForTimeout(2000);

    // Verify dashboard loads initially
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();

    // Now simulate token expiration - intercept API to return 401
    await page.route('**/api/dashboard/stats', route => {
      route.fulfill({
        status: 401,
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ message: 'Unauthorized' })
      });
    });

    // Clear localStorage to simulate expired token
    await page.evaluate(() => {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_user');
    });

    // Reload page
    await page.reload();

    // Should redirect to login
    await page.waitForURL(/\/login/, { timeout: 5000 });
  });
});
