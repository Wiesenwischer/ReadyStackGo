import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Health Monitoring Documentation Screenshots
 * These tests capture screenshots for the public website documentation.
 * They run against a live container with deployed stacks.
 */

test.describe('Health Monitoring - Documentation Screenshots', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('01 - Health Dashboard overview', async ({ page }) => {
    await page.goto('/health');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    await expect(page.getByRole('heading', { name: 'Health Dashboard' })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-01-dashboard-overview.png'),
      fullPage: false,
    });
  });

  test('02 - Health Dashboard summary cards', async ({ page }) => {
    await page.goto('/health');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Verify summary cards are visible
    await expect(page.getByText('Healthy').first()).toBeVisible();
    await expect(page.getByText('Total').first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-02-summary-cards.png'),
      fullPage: false,
    });
  });

  test('03 - Stack card expanded with services', async ({ page }) => {
    await page.goto('/health');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Find and click the stack card to expand
    const stackCard = page.locator('button').filter({ hasText: 'demo-app' });
    if (await stackCard.isVisible()) {
      await stackCard.click();
      await page.waitForTimeout(1000);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-03-stack-expanded.png'),
      fullPage: false,
    });
  });

  test('04 - Deployment detail with health info', async ({ page }) => {
    await page.goto('/deployments/demo-app');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-04-deployment-detail.png'),
      fullPage: false,
    });
  });

  test('05 - Health History chart with timeline', async ({ page }) => {
    await page.goto('/deployments/demo-app');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Scroll to health history section
    const healthHistory = page.getByRole('heading', { name: 'Health History' });
    if (await healthHistory.isVisible()) {
      await healthHistory.scrollIntoViewIfNeeded();
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-05-history-chart.png'),
      fullPage: false,
    });
  });

  test('06 - Services list on deployment detail', async ({ page }) => {
    await page.goto('/deployments/demo-app');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Scroll down to services section
    const servicesSection = page.getByText('Services').first();
    if (await servicesSection.isVisible()) {
      await servicesSection.scrollIntoViewIfNeeded();
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-06-services-list.png'),
      fullPage: false,
    });
  });

  test('07 - Filter by status on dashboard', async ({ page }) => {
    await page.goto('/health');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Click Unhealthy filter
    const unhealthyButton = page.getByRole('button', { name: /^Unhealthy/i });
    if (await unhealthyButton.isVisible()) {
      await unhealthyButton.click();
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-07-filter-status.png'),
      fullPage: false,
    });
  });

  test('08 - Search stacks', async ({ page }) => {
    await page.goto('/health');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    const searchInput = page.getByPlaceholder('Search stacks...');
    if (await searchInput.isVisible()) {
      await searchInput.fill('demo');
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'health-08-search.png'),
      fullPage: false,
    });
  });
});
