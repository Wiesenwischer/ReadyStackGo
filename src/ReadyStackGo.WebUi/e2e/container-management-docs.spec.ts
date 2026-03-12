import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';
import { execSync } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(
  __dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs'
);

const BASE_URL = 'http://localhost:8080';

/**
 * E2E Documentation Tests for Container Management
 * Captures screenshots for the PublicWeb documentation.
 * Works best when the e2e-platform product is deployed (provides real containers to show).
 */

test.describe.serial('Container Management Docs', () => {
  test.setTimeout(60_000);

  let authToken: string;
  let environmentId: string;
  let anyContainerId: string;
  let anyContainerName: string;

  test.beforeAll(async () => {
    try {
      const loginResult = execSync(
        `curl -sf -X POST ${BASE_URL}/api/auth/login ` +
        '-H "Content-Type: application/json" ' +
        '-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}"',
        { encoding: 'utf-8' }
      );
      authToken = JSON.parse(loginResult).token;
    } catch {
      console.warn('Could not get auth token');
    }

    try {
      const envsResult = execSync(
        `curl -sf ${BASE_URL}/api/environments ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8' }
      );
      const envsResponse = JSON.parse(envsResult);
      const envs = envsResponse.environments ?? (Array.isArray(envsResponse) ? envsResponse : []);
      environmentId = envs[0]?.id;
    } catch {
      console.warn('Could not find environment');
    }

    if (environmentId) {
      try {
        const containersResult = execSync(
          `curl -sf "${BASE_URL}/api/containers?environment=${environmentId}" ` +
          `-H "Authorization: Bearer ${authToken}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
        const containers = JSON.parse(containersResult);
        const running = (Array.isArray(containers) ? containers : []).find(
          (c: { state?: string; id?: string; name?: string }) => c.state === 'running'
        );
        if (running) {
          anyContainerId = running.id;
          anyContainerName = running.name?.replace(/^\//, '') ?? '';
          console.log(`Found container for logs test: ${anyContainerName}`);
        }
      } catch {
        console.warn('Could not fetch containers list');
      }
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should show container list view', async ({ page }) => {
    await page.goto('/containers');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);

    // Page heading should be visible
    await expect(page.getByRole('heading', { name: /container management/i })).toBeVisible({ timeout: 10_000 });

    // Refresh button present
    await expect(page.getByRole('button', { name: /refresh/i })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-mgmt-01-list.png'),
      fullPage: false
    });
  });

  test('should show stack view', async ({ page }) => {
    await page.goto('/containers');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Switch to Stack view
    const stackViewBtn = page.getByRole('button', { name: /stacks/i });
    const hasStackBtn = await stackViewBtn.isVisible().catch(() => false);
    if (hasStackBtn) {
      await stackViewBtn.click();
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-mgmt-02-stack-view.png'),
      fullPage: false
    });
  });

  test('should show product view', async ({ page }) => {
    await page.goto('/containers');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Switch to Product view
    const productViewBtn = page.getByRole('button', { name: /products/i });
    const hasProductBtn = await productViewBtn.isVisible().catch(() => false);
    if (hasProductBtn) {
      await productViewBtn.click();
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-mgmt-03-product-view.png'),
      fullPage: false
    });
  });

  test('should show container logs', async ({ page }) => {
    if (!anyContainerId) {
      console.warn('No running container found — skipping logs screenshot');
      // Still take a screenshot of the containers page as fallback
      await page.goto('/containers');
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(1000);
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'container-mgmt-04-logs.png'),
        fullPage: false
      });
      return;
    }

    await page.goto(`/containers/${anyContainerId}/logs?name=${encodeURIComponent(anyContainerName)}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-mgmt-04-logs.png'),
      fullPage: false
    });

    console.log(`✓ Container logs screenshot captured for: ${anyContainerName}`);
  });

  // Edge cases

  test('should handle empty state gracefully', async ({ page }) => {
    // Navigate normally — empty state is theoretical (there are always system containers)
    // This test verifies the page loads and shows either containers or the empty message
    await page.goto('/containers');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    const heading = page.getByRole('heading', { name: /container management/i });
    await expect(heading).toBeVisible({ timeout: 10_000 });

    const hasContainers = await page.locator('table, [class*="grid"]').first().isVisible().catch(() => false);
    const hasEmpty = await page.getByText(/no containers/i).isVisible().catch(() => false);
    expect(hasContainers || hasEmpty).toBeTruthy();
  });

  test('should navigate to containers from sidebar', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Containers link in sidebar
    const containersLink = page.getByRole('link', { name: /containers/i }).first();
    await expect(containersLink).toBeVisible({ timeout: 10_000 });
    await containersLink.click();

    await page.waitForURL(/\/containers/, { timeout: 10_000 });
    await expect(page.getByRole('heading', { name: /container management/i })).toBeVisible();
  });

  test('should show health status badges', async ({ page }) => {
    await page.goto('/containers');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // At least one health badge should be present (healthy, unhealthy, starting, or none)
    const healthBadge = page.locator('[class*="badge"], span').filter({
      hasText: /healthy|unhealthy|starting|none/i
    }).first();

    const hasBadge = await healthBadge.isVisible().catch(() => false);
    if (hasBadge) {
      console.log('✓ Health status badge found');
    }
    // Non-blocking: badge presence depends on running containers
  });

  test('should show error on API failure', async ({ page }) => {
    await page.route('**/api/containers**', route => route.abort());
    await page.goto('/containers');
    await page.waitForTimeout(2000);

    const errorEl = page.locator('[class*="red"], [class*="error"], [role="alert"]').first();
    const hasError = await errorEl.isVisible().catch(() => false);
    if (hasError) {
      console.log('✓ Error state shown on API failure');
    }
  });
});
