import { test, expect } from '@playwright/test';

/**
 * E2E Tests für Stack Actions (Deploy/Remove)
 * Diese Tests testen die Deploy- und Remove-Funktionalität
 */

test.describe('Stack Actions', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/stacks');
    // Wait for stacks to load
    await page.waitForTimeout(2000);
  });

  test('should deploy a stack successfully', async ({ page }) => {
    // First ensure stack is not deployed by clicking Remove if it exists
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      await removeButton.click();
      // Wait for removal to complete
      await page.waitForTimeout(3000);
    }

    // Now deploy the stack
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    await expect(deployButton).toBeVisible();
    await deployButton.click();

    // Should show "Deploying..." state
    await expect(page.getByText(/deploying\.\.\./i)).toBeVisible();

    // Wait for deployment to complete (up to 15 seconds)
    await page.waitForTimeout(15000);

    // Should show Running status
    const runningBadge = page.locator('span').filter({ hasText: 'Running' });
    await expect(runningBadge).toBeVisible({ timeout: 5000 });

    // Deploy button should be replaced with Remove button
    const removeBtn = page.getByRole('button', { name: /^remove$/i }).first();
    await expect(removeBtn).toBeVisible();

    // Cleanup - remove the stack
    await removeBtn.click();
    await page.waitForTimeout(3000);
  });

  test('should remove a deployed stack successfully', async ({ page }) => {
    // First ensure stack is deployed
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    const hasDeployButton = await deployButton.isVisible().catch(() => false);

    if (hasDeployButton) {
      await deployButton.click();
      // Wait for deployment to complete
      await page.waitForTimeout(15000);
    }

    // Now remove the stack
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    await expect(removeButton).toBeVisible();
    await removeButton.click();

    // Should show "Removing..." state
    await expect(page.getByText(/removing\.\.\./i)).toBeVisible();

    // Wait for removal to complete
    await page.waitForTimeout(5000);

    // Should show NotDeployed status
    const notDeployedBadge = page.locator('span').filter({ hasText: 'NotDeployed' });
    await expect(notDeployedBadge).toBeVisible({ timeout: 5000 });

    // Remove button should be replaced with Deploy button
    const deployBtn = page.getByRole('button', { name: /^deploy$/i }).first();
    await expect(deployBtn).toBeVisible();
  });

  test('should disable button during deployment', async ({ page }) => {
    // Ensure stack is not deployed
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      await removeButton.click();
      await page.waitForTimeout(3000);
    }

    // Click deploy
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    await deployButton.click();

    // Button should be disabled during deployment
    const deployingButton = page.getByRole('button', { name: /deploying/i }).first();
    await expect(deployingButton).toBeDisabled();

    // Wait for deployment to complete
    await page.waitForTimeout(15000);

    // Cleanup
    const removeBtn = page.getByRole('button', { name: /^remove$/i }).first();
    if (await removeBtn.isVisible().catch(() => false)) {
      await removeBtn.click();
      await page.waitForTimeout(3000);
    }
  });

  test('should disable button during removal', async ({ page }) => {
    // Ensure stack is deployed
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    const hasDeployButton = await deployButton.isVisible().catch(() => false);

    if (hasDeployButton) {
      await deployButton.click();
      await page.waitForTimeout(15000);
    }

    // Click remove
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    await removeButton.click();

    // Button should be disabled during removal
    const removingButton = page.getByRole('button', { name: /removing/i }).first();
    await expect(removingButton).toBeDisabled();

    // Wait for removal to complete
    await page.waitForTimeout(5000);
  });

  test('should handle deploy error gracefully', async ({ page }) => {
    // Mock API failure for deployment
    await page.route('**/api/stacks/*/deploy', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ message: 'Internal Server Error' })
      });
    });

    // Ensure stack is not deployed
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      await removeButton.click();
      await page.waitForTimeout(3000);
      // Reset route after removal
      await page.unroute('**/api/stacks/*/deploy');
      await page.route('**/api/stacks/*/deploy', route => {
        route.fulfill({
          status: 500,
          body: JSON.stringify({ message: 'Internal Server Error' })
        });
      });
    }

    // Try to deploy
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    await deployButton.click();

    // Wait for error
    await page.waitForTimeout(2000);

    // Should show error message
    const errorMessage = page.locator('[class*="red"]').filter({ hasText: /failed/i });
    await expect(errorMessage).toBeVisible();
  });

  test('should refresh stack list after successful deployment', async ({ page }) => {
    // Ensure stack is not deployed
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      await removeButton.click();
      await page.waitForTimeout(3000);
    }

    // Get initial status
    const initialStatus = await page.locator('span[class*="rounded-full"]').first().textContent();

    // Deploy
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    await deployButton.click();
    await page.waitForTimeout(15000);

    // Get new status
    const newStatus = await page.locator('span[class*="rounded-full"]').first().textContent();

    // Status should have changed from NotDeployed to Running
    expect(initialStatus).toContain('NotDeployed');
    expect(newStatus).toContain('Running');

    // Cleanup
    const removeBtn = page.getByRole('button', { name: /^remove$/i }).first();
    if (await removeBtn.isVisible().catch(() => false)) {
      await removeBtn.click();
      await page.waitForTimeout(3000);
    }
  });

  test('full stack lifecycle: deploy and remove', async ({ page }) => {
    // 1. Start with NotDeployed state (cleanup first)
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      await removeButton.click();
      await page.waitForTimeout(3000);
    }

    // Verify NotDeployed state
    const notDeployedBadge = page.locator('span').filter({ hasText: 'NotDeployed' });
    await expect(notDeployedBadge).toBeVisible();

    // 2. Deploy the stack
    const deployButton = page.getByRole('button', { name: /^deploy$/i }).first();
    await deployButton.click();
    await page.waitForTimeout(15000);

    // 3. Verify Running state
    const runningBadge = page.locator('span').filter({ hasText: 'Running' });
    await expect(runningBadge).toBeVisible();

    // 4. Remove the stack
    const removeBtn = page.getByRole('button', { name: /^remove$/i }).first();
    await removeBtn.click();
    await page.waitForTimeout(5000);

    // 5. Verify back to NotDeployed state
    await expect(notDeployedBadge).toBeVisible();
  });
});
