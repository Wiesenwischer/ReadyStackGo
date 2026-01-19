import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';

/**
 * E2E Tests for Health Dashboard
 * Tests the complete user journey for health monitoring functionality
 */

test.describe('Health Dashboard - Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto('/');
    await page.waitForTimeout(1000);
  });

  test('should have Health link in sidebar navigation', async ({ page }) => {
    // Check that Health navigation link exists in sidebar
    const healthLink = page.locator('nav').getByRole('link', { name: 'Health' });
    await expect(healthLink).toBeVisible();
  });

  test('should navigate to /health when clicking Health link', async ({ page }) => {
    // Click on Health in sidebar
    const healthLink = page.locator('nav').getByRole('link', { name: 'Health' });
    await healthLink.click();

    // Should navigate to /health
    await page.waitForURL('/health');
    await expect(page).toHaveURL('/health');
  });
});

test.describe('Health Dashboard - Page Content', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto('/health');
    await page.waitForTimeout(2000);
  });

  test('should display Health Dashboard title', async ({ page }) => {
    const title = page.getByRole('heading', { name: 'Health Dashboard' });
    await expect(title).toBeVisible();
  });

  test('should display summary cards', async ({ page }) => {
    // Check for Healthy card
    await expect(page.getByText('Healthy').first()).toBeVisible();

    // Check for Degraded card
    await expect(page.getByText('Degraded').first()).toBeVisible();

    // Check for Unhealthy card
    await expect(page.getByText('Unhealthy').first()).toBeVisible();

    // Check for Total card
    await expect(page.getByText('Total').first()).toBeVisible();
  });

  test('should display connection status indicator', async ({ page }) => {
    // Should show either "Live", "Connecting..." or "Offline"
    const liveIndicator = page.getByText('Live');
    const connectingIndicator = page.getByText('Connecting...');
    const offlineIndicator = page.getByText('Offline');

    // At least one should be visible
    const isLive = await liveIndicator.isVisible().catch(() => false);
    const isConnecting = await connectingIndicator.isVisible().catch(() => false);
    const isOffline = await offlineIndicator.isVisible().catch(() => false);

    expect(isLive || isConnecting || isOffline).toBeTruthy();
  });

  test('should display Refresh button', async ({ page }) => {
    const refreshButton = page.getByRole('button', { name: 'Refresh' });
    await expect(refreshButton).toBeVisible();
  });
});

test.describe('Health Dashboard - Filters', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto('/health');
    await page.waitForTimeout(2000);
  });

  test('should display filter buttons', async ({ page }) => {
    // Check for status filter buttons
    await expect(page.getByRole('button', { name: /^All/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Healthy/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Degraded/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Unhealthy/i })).toBeVisible();
  });

  test('should display search input', async ({ page }) => {
    const searchInput = page.getByPlaceholder('Search stacks...');
    await expect(searchInput).toBeVisible();
  });

  test('should filter by clicking status button', async ({ page }) => {
    // Click on Healthy filter
    const healthyButton = page.getByRole('button', { name: /^Healthy/i });
    await healthyButton.click();

    // Button should be highlighted (has brand-500 background)
    await expect(healthyButton).toHaveClass(/bg-brand-500/);
  });

  test('should search stacks by name', async ({ page }) => {
    const searchInput = page.getByPlaceholder('Search stacks...');

    // Type a search term
    await searchInput.fill('test');

    // Wait for filter to apply
    await page.waitForTimeout(500);

    // Search input should have the value
    await expect(searchInput).toHaveValue('test');
  });
});

test.describe('Health Dashboard - Empty State', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should show empty state when no deployments exist', async ({ page }) => {
    // Mock API to return no stacks
    await page.route('**/api/health/**', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            environmentId: 'test-env',
            environmentName: 'Test',
            totalStacks: 0,
            healthyCount: 0,
            degradedCount: 0,
            unhealthyCount: 0,
            stacks: []
          }
        })
      });
    });

    await page.goto('/health');
    await page.waitForTimeout(2000);

    // Should show "No Deployments" message
    const emptyMessage = page.getByText(/No Deployments|No Matching Stacks/i);
    await expect(emptyMessage).toBeVisible();
  });
});

test.describe('Health Dashboard - Error Handling', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should display error message when API fails', async ({ page }) => {
    // Mock API to return error
    await page.route('**/api/health/**', route => {
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          message: 'Internal Server Error'
        })
      });
    });

    await page.goto('/health');
    await page.waitForTimeout(2000);

    // Should show error message
    const errorMessage = page.getByText(/Error|Failed/i);
    await expect(errorMessage).toBeVisible();
  });
});

test.describe('Health Dashboard - Stack Cards', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);

    // Mock API with test data
    await page.route('**/api/health/**', route => {
      const url = route.request().url();

      // Environment health summary
      if (url.includes('/deployments/') && url.includes('forceRefresh')) {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            data: {
              deploymentId: 'test-deployment-1',
              environmentId: 'test-env',
              stackName: 'test-stack',
              currentVersion: '1.0.0',
              overallStatus: 'Healthy',
              operationMode: 'Normal',
              statusMessage: 'All services healthy',
              requiresAttention: false,
              capturedAtUtc: new Date().toISOString(),
              self: {
                status: 'Healthy',
                healthyCount: 2,
                totalCount: 2,
                services: [
                  { name: 'api', status: 'Healthy', containerId: 'abc123', containerName: 'test-api', reason: null, restartCount: 0 },
                  { name: 'db', status: 'Healthy', containerId: 'def456', containerName: 'test-db', reason: null, restartCount: 0 }
                ]
              },
              bus: null,
              infra: null
            }
          })
        });
      } else {
        // Environment summary
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            data: {
              environmentId: 'test-env',
              environmentName: 'Test',
              totalStacks: 1,
              healthyCount: 1,
              degradedCount: 0,
              unhealthyCount: 0,
              stacks: [
                {
                  deploymentId: 'test-deployment-1',
                  stackName: 'test-stack',
                  currentVersion: '1.0.0',
                  overallStatus: 'Healthy',
                  operationMode: 'Normal',
                  healthyServices: 2,
                  totalServices: 2,
                  statusMessage: 'All services healthy',
                  requiresAttention: false,
                  capturedAtUtc: new Date().toISOString()
                }
              ]
            }
          })
        });
      }
    });

    await page.goto('/health');
    await page.waitForTimeout(2000);
  });

  test('should display stack card with name and status', async ({ page }) => {
    // Check for stack name
    await expect(page.getByText('test-stack')).toBeVisible();

    // Check for health status badge
    await expect(page.getByText('Healthy').first()).toBeVisible();

    // Check for service count
    await expect(page.getByText('2/2 services')).toBeVisible();
  });

  test('should expand stack card on click', async ({ page }) => {
    // Click on the stack card to expand
    const stackCard = page.locator('button').filter({ hasText: 'test-stack' });
    await stackCard.click();

    // Wait for expansion animation
    await page.waitForTimeout(500);

    // Should show service details
    await expect(page.getByText('api')).toBeVisible();
    await expect(page.getByText('db')).toBeVisible();
  });

  test('should have link to deployment detail in expanded card', async ({ page }) => {
    // Expand the stack card
    const stackCard = page.locator('button').filter({ hasText: 'test-stack' });
    await stackCard.click();

    await page.waitForTimeout(500);

    // Should show "View Details" link
    const viewDetailsLink = page.getByRole('link', { name: 'View Details' });
    await expect(viewDetailsLink).toBeVisible();
  });
});

test.describe('Health Dashboard - Unauthenticated Access', () => {
  test('should redirect to login when not authenticated', async ({ page }) => {
    // Clear any auth
    await page.goto('/login');
    await page.evaluate(() => {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_user');
    });

    // Try to access health dashboard
    await page.goto('/health');

    // Should redirect to login
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/\/login/);
  });
});
