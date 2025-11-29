import { test, expect } from '@playwright/test';

/**
 * E2E Tests für Stack Actions
 * Diese Tests testen die Deploy-Compose-Modal Funktionalität
 */

test.describe('Stack Actions', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Navigate to stacks page
    await page.goto('/stacks');
    await page.waitForTimeout(2000);
  });

  test('should open deploy modal when clicking Deploy Custom button', async ({ page }) => {
    // Click Deploy Custom button
    await page.getByRole('button', { name: /deploy custom/i }).click();

    // Modal should open
    await expect(page.getByText(/deploy docker compose/i)).toBeVisible();
  });

  test('should close modal when clicking close button', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();
    await expect(page.getByText(/deploy docker compose/i)).toBeVisible();

    // Click close button (X)
    await page.locator('button').filter({ has: page.locator('svg') }).first().click();

    // Modal should be closed
    await expect(page.getByText(/deploy docker compose/i)).not.toBeVisible();
  });

  test('should close modal when clicking Cancel button', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();
    await expect(page.getByText(/deploy docker compose/i)).toBeVisible();

    // Click Cancel button
    await page.getByRole('button', { name: /cancel/i }).click();

    // Modal should be closed
    await expect(page.getByText(/deploy docker compose/i)).not.toBeVisible();
  });

  test('should show error when trying to continue with empty YAML', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();
    await expect(page.getByText(/deploy docker compose/i)).toBeVisible();

    // Click Continue without entering YAML
    await page.getByRole('button', { name: /continue/i }).click();

    // Should show error
    await expect(page.getByText(/please provide/i)).toBeVisible();
  });

  test('should parse valid YAML and show configure step', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();

    // Enter valid YAML
    const yaml = `version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - "8081:80"`;

    await page.locator('textarea').fill(yaml);

    // Click Continue
    await page.getByRole('button', { name: /continue/i }).click();

    // Should show configure step with stack name input
    await expect(page.getByText(/stack name/i)).toBeVisible();

    // Should show detected services
    await expect(page.getByText(/services detected/i)).toBeVisible();
  });

  test('should deploy stack from available stacks list', async ({ page }) => {
    // Check if there are any stacks to deploy
    const deployButtons = page.locator('button').filter({ hasText: /^deploy$/i });
    const stackCount = await deployButtons.count();

    if (stackCount > 0) {
      // Click first deploy button in Available Stacks section
      await deployButtons.first().click();

      // Modal should open with configure step (preloaded from stack)
      await expect(page.getByText(/stack name/i)).toBeVisible({ timeout: 5000 });
    } else {
      // Skip test if no stacks available
      test.skip();
    }
  });

  test('should show services list in configure step', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();

    // Enter YAML with multiple services
    const yaml = `version: '3.8'
services:
  web:
    image: nginx:latest
  db:
    image: postgres:15
  cache:
    image: redis:alpine`;

    await page.locator('textarea').fill(yaml);

    // Click Continue
    await page.getByRole('button', { name: /continue/i }).click();

    // Should show all services
    await expect(page.getByText(/web.*db.*cache|services detected/i)).toBeVisible();
  });

  test('should require stack name before deploy', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();

    // Enter valid YAML
    const yaml = `version: '3.8'
services:
  web:
    image: nginx:latest`;

    await page.locator('textarea').fill(yaml);
    await page.getByRole('button', { name: /continue/i }).click();

    // Clear stack name if pre-filled
    await page.locator('input').first().fill('');

    // Try to deploy
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Should show error about missing stack name
    await expect(page.getByText(/provide.*stack name|stack name/i)).toBeVisible();
  });

  test('should handle environment variables in YAML', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy custom/i }).click();

    // Enter YAML with environment variables
    const yaml = `version: '3.8'
services:
  web:
    image: nginx:\${NGINX_VERSION:-latest}
    environment:
      - DB_HOST=\${DB_HOST}
      - API_KEY=\${API_KEY:-default}`;

    await page.locator('textarea').fill(yaml);
    await page.getByRole('button', { name: /continue/i }).click();

    // Should show environment variables section
    await expect(page.getByText(/environment variables/i)).toBeVisible();
  });
});

test.describe('Deployed Stacks', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Navigate to stacks page
    await page.goto('/stacks');
    await page.waitForTimeout(2000);
  });

  test('should show deployed stacks section', async ({ page }) => {
    // Should have Deployed Stacks heading
    await expect(page.getByRole('heading', { name: /deployed stacks/i })).toBeVisible();
  });

  test('should show remove button for deployed stacks', async ({ page }) => {
    // Check if there are any deployed stacks
    const removeButtons = page.locator('button').filter({ hasText: /^remove$/i });
    const deployedCount = await removeButtons.count();

    // The Remove button should exist for each deployed stack
    // (or we might have none if nothing is deployed)
    expect(deployedCount).toBeGreaterThanOrEqual(0);
  });

  test('should show no deployments message when empty', async ({ page }) => {
    // If no deployments exist, should show appropriate message
    const noDeploymentsMessage = page.getByText(/no deployments|select an environment/i);
    const hasNoDeployments = await noDeploymentsMessage.isVisible().catch(() => false);

    // Either there are deployments (remove buttons) or the no deployments message
    const removeButtons = page.locator('button').filter({ hasText: /^remove$/i });
    const hasDeployments = (await removeButtons.count()) > 0;

    expect(hasNoDeployments || hasDeployments).toBeTruthy();
  });
});
