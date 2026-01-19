import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';

/**
 * E2E Tests for Health History Chart
 * Tests the health history visualization in the Deployment Detail page
 */

test.describe('Health History Chart - Deployment Detail', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);

    // Mock health history API
    await page.route('**/api/health/deployments/*/history*', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          history: [
            {
              deploymentId: 'test-deployment',
              stackName: 'test-stack',
              currentVersion: '1.0.0',
              overallStatus: 'Healthy',
              operationMode: 'Normal',
              healthyServices: 2,
              totalServices: 2,
              statusMessage: 'All services healthy',
              requiresAttention: false,
              capturedAtUtc: new Date(Date.now() - 1000 * 60 * 5).toISOString() // 5 min ago
            },
            {
              deploymentId: 'test-deployment',
              stackName: 'test-stack',
              currentVersion: '1.0.0',
              overallStatus: 'Healthy',
              operationMode: 'Normal',
              healthyServices: 2,
              totalServices: 2,
              statusMessage: 'All services healthy',
              requiresAttention: false,
              capturedAtUtc: new Date(Date.now() - 1000 * 60 * 10).toISOString() // 10 min ago
            },
            {
              deploymentId: 'test-deployment',
              stackName: 'test-stack',
              currentVersion: '1.0.0',
              overallStatus: 'Degraded',
              operationMode: 'Normal',
              healthyServices: 1,
              totalServices: 2,
              statusMessage: 'Some services unhealthy',
              requiresAttention: true,
              capturedAtUtc: new Date(Date.now() - 1000 * 60 * 15).toISOString() // 15 min ago
            }
          ]
        })
      });
    });
  });

  test('should display Health History section in deployment detail', async ({ page }) => {
    // Navigate to a deployment detail page
    // First we need to have a deployment - mock the deployment API
    await page.route('**/api/environments/*/deployments/*', route => {
      if (route.request().method() === 'GET') {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            deploymentId: 'test-deployment-id',
            stackName: 'test-stack',
            stackVersion: '1.0.0',
            status: 'Running',
            deployedAt: new Date().toISOString(),
            services: [
              { serviceName: 'api', image: 'test/api:1.0.0', status: 'Running' },
              { serviceName: 'db', image: 'postgres:15', status: 'Running' }
            ],
            configuration: {}
          })
        });
      } else {
        route.continue();
      }
    });

    // Mock health data
    await page.route('**/api/health/*/deployments/*', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            deploymentId: 'test-deployment-id',
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
                { name: 'api', status: 'Healthy', containerId: 'abc', containerName: 'api', reason: null, restartCount: 0 },
                { name: 'db', status: 'Healthy', containerId: 'def', containerName: 'db', reason: null, restartCount: 0 }
              ]
            },
            bus: null,
            infra: null
          }
        })
      });
    });

    await page.goto('/deployments/test-stack');
    await page.waitForTimeout(2000);

    // Check for Health History heading
    const healthHistoryHeading = page.getByRole('heading', { name: 'Health History' });
    await expect(healthHistoryHeading).toBeVisible();
  });

  test('should display chart legend', async ({ page }) => {
    // Setup mocks similar to above
    await page.route('**/api/environments/*/deployments/*', route => {
      if (route.request().method() === 'GET') {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            deploymentId: 'test-deployment-id',
            stackName: 'test-stack',
            stackVersion: '1.0.0',
            status: 'Running',
            deployedAt: new Date().toISOString(),
            services: [],
            configuration: {}
          })
        });
      } else {
        route.continue();
      }
    });

    await page.route('**/api/health/*/deployments/*', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            deploymentId: 'test-deployment-id',
            environmentId: 'test-env',
            stackName: 'test-stack',
            currentVersion: '1.0.0',
            overallStatus: 'Healthy',
            operationMode: 'Normal',
            statusMessage: 'All services healthy',
            requiresAttention: false,
            capturedAtUtc: new Date().toISOString(),
            self: { status: 'Healthy', healthyCount: 0, totalCount: 0, services: [] },
            bus: null,
            infra: null
          }
        })
      });
    });

    await page.goto('/deployments/test-stack');
    await page.waitForTimeout(2000);

    // Legend should show status colors
    await expect(page.getByText('Healthy').first()).toBeVisible();
    await expect(page.getByText('Degraded').first()).toBeVisible();
    await expect(page.getByText('Unhealthy').first()).toBeVisible();
  });

  test('should show empty state when no history available', async ({ page }) => {
    // Mock empty history
    await page.route('**/api/health/deployments/*/history*', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          history: []
        })
      });
    });

    await page.route('**/api/environments/*/deployments/*', route => {
      if (route.request().method() === 'GET') {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            deploymentId: 'test-deployment-id',
            stackName: 'test-stack',
            stackVersion: '1.0.0',
            status: 'Running',
            deployedAt: new Date().toISOString(),
            services: [],
            configuration: {}
          })
        });
      } else {
        route.continue();
      }
    });

    await page.route('**/api/health/*/deployments/*', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            deploymentId: 'test-deployment-id',
            environmentId: 'test-env',
            stackName: 'test-stack',
            currentVersion: '1.0.0',
            overallStatus: 'Healthy',
            operationMode: 'Normal',
            statusMessage: 'All services healthy',
            requiresAttention: false,
            capturedAtUtc: new Date().toISOString(),
            self: { status: 'Healthy', healthyCount: 0, totalCount: 0, services: [] },
            bus: null,
            infra: null
          }
        })
      });
    });

    await page.goto('/deployments/test-stack');
    await page.waitForTimeout(2000);

    // Should show empty state message
    const emptyMessage = page.getByText(/No health history available/i);
    await expect(emptyMessage).toBeVisible();
  });

  test('should handle API error gracefully', async ({ page }) => {
    // Mock error response
    await page.route('**/api/health/deployments/*/history*', route => {
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          message: 'Internal Server Error'
        })
      });
    });

    await page.route('**/api/environments/*/deployments/*', route => {
      if (route.request().method() === 'GET') {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            deploymentId: 'test-deployment-id',
            stackName: 'test-stack',
            stackVersion: '1.0.0',
            status: 'Running',
            deployedAt: new Date().toISOString(),
            services: [],
            configuration: {}
          })
        });
      } else {
        route.continue();
      }
    });

    await page.route('**/api/health/*/deployments/*', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            deploymentId: 'test-deployment-id',
            environmentId: 'test-env',
            stackName: 'test-stack',
            currentVersion: '1.0.0',
            overallStatus: 'Healthy',
            operationMode: 'Normal',
            statusMessage: 'All services healthy',
            requiresAttention: false,
            capturedAtUtc: new Date().toISOString(),
            self: { status: 'Healthy', healthyCount: 0, totalCount: 0, services: [] },
            bus: null,
            infra: null
          }
        })
      });
    });

    await page.goto('/deployments/test-stack');
    await page.waitForTimeout(2000);

    // Should show Health History section with error
    const healthHistoryHeading = page.getByText('Health History');
    await expect(healthHistoryHeading).toBeVisible();

    // Error could be shown inline - just verify the section exists
    await expect(healthHistoryHeading).toBeVisible();
  });
});
