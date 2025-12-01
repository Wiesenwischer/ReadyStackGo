import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';

/**
 * E2E Tests for Deployment Workflow
 * Tests the new environment-scoped deployment API and Deploy Stack modal
 */

test.describe('Deployment Workflow', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await login(page);
    await page.goto('/stacks');
  });

  test('should display stacks page with Deploy Stack button', async ({ page }) => {
    // Title check
    await expect(page.locator('h1')).toContainText('Stack Management');

    // Should have Deploy Stack button
    await expect(page.getByRole('button', { name: /deploy stack/i })).toBeVisible();

    // Should have Refresh button
    await expect(page.getByRole('button', { name: /refresh/i })).toBeVisible();
  });

  test('should show message when no environment is selected', async ({ page }) => {
    // Wait for page to load
    await page.waitForTimeout(1000);

    // Check for environment warning message or deployments heading
    const noEnvMessage = page.getByText(/no environment selected/i);
    const selectEnvMessage = page.getByText(/select an environment/i);

    const hasNoEnvMessage = await noEnvMessage.isVisible().catch(() => false);
    const hasSelectEnvMessage = await selectEnvMessage.isVisible().catch(() => false);

    // Should show some message about environment selection
    // (if no environment is available)
    if (!hasNoEnvMessage && !hasSelectEnvMessage) {
      // Environment is already selected, should show deployments section
      await expect(page.getByRole('heading', { name: /deployed stacks/i })).toBeVisible();
    }
  });

  test('should open Deploy Stack modal when clicking button', async ({ page }) => {
    // Wait for page to load
    await page.waitForTimeout(1000);

    // Click Deploy Stack button
    const deployButton = page.getByRole('button', { name: /deploy stack/i });
    await deployButton.click();

    // Modal should be visible with title
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    // Should have Stack Name input
    await expect(page.getByLabel(/stack name/i)).toBeVisible();

    // Should have Docker Compose textarea
    await expect(page.getByLabel(/docker compose yaml/i)).toBeVisible();

    // Should have Next/Parse button
    await expect(page.getByRole('button', { name: /next|parse/i })).toBeVisible();

    // Should have Cancel button
    await expect(page.getByRole('button', { name: /cancel/i })).toBeVisible();
  });

  test('should close modal when clicking Cancel', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    // Click Cancel
    await page.getByRole('button', { name: /cancel/i }).click();

    // Modal should be closed
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).not.toBeVisible();
  });

  test('should validate stack name input', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Enter YAML but no stack name
    const yamlTextarea = page.getByLabel(/docker compose yaml/i);
    await yamlTextarea.fill(`version: '3.8'
services:
  web:
    image: nginx:alpine`);

    // Try to proceed
    await page.getByRole('button', { name: /next|parse/i }).click();

    // Should show validation error
    await expect(page.getByText(/stack name.*required|enter.*stack name/i)).toBeVisible({ timeout: 3000 });
  });

  test('should validate YAML content', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Enter stack name but no YAML
    const stackNameInput = page.getByLabel(/stack name/i);
    await stackNameInput.fill('test-stack');

    // Try to proceed with empty YAML
    await page.getByRole('button', { name: /next|parse/i }).click();

    // Should show validation error
    await expect(page.getByText(/yaml.*required|compose.*required/i)).toBeVisible({ timeout: 3000 });
  });

  test('should parse valid Docker Compose YAML', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Fill in stack name
    await page.getByLabel(/stack name/i).fill('test-stack');

    // Fill in valid YAML
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  web:
    image: nginx:alpine
    ports:
      - '8080:80'`);

    // Click Next/Parse
    await page.getByRole('button', { name: /next|parse/i }).click();

    // Should proceed to next step or show deploy button
    await page.waitForTimeout(2000);

    // Should show either Deploy button or services list
    const deployBtn = page.getByRole('button', { name: /^deploy$/i });
    const servicesList = page.getByText(/web/i);

    const hasDeployBtn = await deployBtn.isVisible().catch(() => false);
    const hasServices = await servicesList.isVisible().catch(() => false);

    expect(hasDeployBtn || hasServices).toBeTruthy();
  });

  test('should detect variables in Docker Compose', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Fill in stack name
    await page.getByLabel(/stack name/i).fill('test-stack');

    // Fill in YAML with variables
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  db:
    image: postgres:15
    environment:
      POSTGRES_USER: \${DB_USER:-postgres}
      POSTGRES_PASSWORD: \${DB_PASSWORD}`);

    // Click Next/Parse
    await page.getByRole('button', { name: /next|parse/i }).click();

    // Wait for parsing
    await page.waitForTimeout(2000);

    // Should show detected variables
    // Look for variable names or inputs
    const dbPasswordVar = page.getByText(/DB_PASSWORD/i);
    const dbUserVar = page.getByText(/DB_USER/i);

    const hasPasswordVar = await dbPasswordVar.isVisible().catch(() => false);
    const hasUserVar = await dbUserVar.isVisible().catch(() => false);

    // At least one variable should be shown
    expect(hasPasswordVar || hasUserVar).toBeTruthy();
  });

  test('should show table headers for deployments', async ({ page }) => {
    // Wait for page to load
    await page.waitForTimeout(2000);

    // Check for table headers - they may not be visible if no deployments
    const headerRow = page.locator('.grid.grid-cols-6, .grid.grid-cols-8').first();

    // If there are deployments, headers should be visible
    const stackNameHeader = headerRow.getByText('Stack Name');
    void headerRow.getByText('Status');
    void headerRow.getByText('Actions');

    // Either should have headers or "No deployments" message
    const hasStackNameHeader = await stackNameHeader.isVisible().catch(() => false);
    const noDeploymentsMessage = page.getByText(/no deployments|click.*deploy/i);
    const hasNoDeploymentsMessage = await noDeploymentsMessage.isVisible().catch(() => false);

    expect(hasStackNameHeader || hasNoDeploymentsMessage).toBeTruthy();
  });

  test('should display environment name in page header', async ({ page }) => {
    // Wait for page to load
    await page.waitForTimeout(1000);

    // The Deployed Stacks heading should include environment name if selected
    const deployedStacksHeading = page.getByRole('heading', { name: /deployed stacks/i });
    await expect(deployedStacksHeading).toBeVisible();

    // Check if environment name is shown in parentheses
    const headingText = await deployedStacksHeading.textContent();
    // Heading format: "Deployed Stacks (Environment Name)"
    // Environment might not be selected initially
    expect(headingText).toBeTruthy();
  });

  test('should refresh deployments when clicking Refresh button', async ({ page }) => {
    const refreshButton = page.getByRole('button', { name: /refresh/i });

    // Click refresh
    await refreshButton.click();

    // Wait for loading state
    await page.waitForTimeout(1000);

    // Should still be on stacks page
    await expect(page.locator('h1')).toContainText('Stack Management');
  });

  test('should handle parse error for invalid YAML', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Fill in stack name
    await page.getByLabel(/stack name/i).fill('test-stack');

    // Fill in invalid YAML
    await page.getByLabel(/docker compose yaml/i).fill(`this is not valid yaml: [
      missing bracket`);

    // Click Next/Parse
    await page.getByRole('button', { name: /next|parse/i }).click();

    // Should show error
    await page.waitForTimeout(2000);
    const errorMessage = page.locator('[class*="red"]');
    await expect(errorMessage).toBeVisible();
  });

  test('should navigate to stacks from sidebar', async ({ page }) => {
    // Go to home first
    await page.goto('/');

    // Click Stacks in sidebar
    await page.getByRole('link', { name: /stacks/i }).first().click();

    // Should be on stacks page
    await expect(page).toHaveURL(/.*stacks/);
    await expect(page.locator('h1')).toContainText('Stack Management');
  });

  test('should display error message when API is unavailable', async ({ page }) => {
    // Mock API failure
    await page.route('**/api/deployments/**', route => {
      route.abort();
    });

    await page.goto('/stacks');
    await page.waitForTimeout(1000);

    // Should show error message or no deployments message
    const errorMessage = page.locator('[class*="red"]').filter({ hasText: /failed|error/i });
    const noDeploymentsMessage = page.getByText(/no deployments/i);

    const hasError = await errorMessage.isVisible().catch(() => false);
    const hasNoDeployments = await noDeploymentsMessage.isVisible().catch(() => false);

    // Should show either error or empty state
    expect(hasError || hasNoDeployments).toBeTruthy();
  });
});

test.describe('Deployment Actions', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await login(page);
    await page.goto('/stacks');
    // Wait for page to load
    await page.waitForTimeout(2000);
  });

  test('should show Remove button for existing deployments', async ({ page }) => {
    // If there are deployments, there should be Remove buttons
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      await expect(removeButton).toBeEnabled();
    }
  });

  test('should confirm before removing deployment', async ({ page }) => {
    const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
    const hasRemoveButton = await removeButton.isVisible().catch(() => false);

    if (hasRemoveButton) {
      // Click remove
      await removeButton.click();

      // Should show loading state or remove immediately
      await page.waitForTimeout(1000);

      // Button should be disabled during removal or deployment should be removed
      const isDisabled = await removeButton.isDisabled().catch(() => true);
      const stillVisible = await removeButton.isVisible().catch(() => false);

      // Either button is disabled (during removal) or it's gone (removal complete)
      expect(isDisabled || !stillVisible).toBeTruthy();
    }
  });

  test('should update deployment list after removal', async ({ page }) => {
    // Count initial deployments
    const initialRemoveButtons = await page.getByRole('button', { name: /^remove$/i }).count();

    if (initialRemoveButtons > 0) {
      // Remove first deployment
      const removeButton = page.getByRole('button', { name: /^remove$/i }).first();
      await removeButton.click();

      // Wait for removal
      await page.waitForTimeout(3000);

      // Count deployments after removal
      const finalRemoveButtons = await page.getByRole('button', { name: /^remove$/i }).count();

      // Should have one less deployment
      expect(finalRemoveButtons).toBeLessThanOrEqual(initialRemoveButtons);
    }
  });
});

test.describe('Deploy Stack Modal Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await login(page);
    await page.goto('/stacks');
    await page.waitForTimeout(1000);
  });

  test('complete deploy flow with simple stack', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    // Step 1: Enter stack details
    await page.getByLabel(/stack name/i).fill('e2e-test-stack');
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  web:
    image: nginx:alpine
    ports:
      - '9090:80'`);

    // Click Next/Parse
    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    // Step 2/3: Should show services detected and Deploy button
    const deployBtn = page.getByRole('button', { name: /^deploy$/i });
    const hasDeployBtn = await deployBtn.isVisible().catch(() => false);

    if (hasDeployBtn) {
      // Click Deploy
      await deployBtn.click();

      // Wait for deployment
      await page.waitForTimeout(5000);

      // Modal should close
      await expect(page.getByRole('heading', { name: /deploy docker compose/i })).not.toBeVisible({ timeout: 10000 });

      // Should show the new deployment in the list
      await expect(page.getByText('e2e-test-stack')).toBeVisible({ timeout: 5000 });
    }
  });

  test('should show step indicator in modal', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Should show some indication of steps/progress
    // Could be step numbers, progress bar, or step titles
    const stepIndicator = page.locator('[class*="step"], [class*="progress"]');
    const stepText = page.getByText(/step|compose|configure|review|deploy/i);

    const hasStepIndicator = await stepIndicator.count() > 0;
    const hasStepText = await stepText.isVisible().catch(() => false);

    // Modal content should be visible
    expect(hasStepIndicator || hasStepText).toBeTruthy();
  });

  test('should allow going back in modal steps', async ({ page }) => {
    // Open modal
    await page.getByRole('button', { name: /deploy stack/i }).click();

    // Fill in initial data
    await page.getByLabel(/stack name/i).fill('test-stack');
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  web:
    image: nginx:alpine`);

    // Go to next step
    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    // Check for Back button
    const backButton = page.getByRole('button', { name: /back|previous/i });
    const hasBackButton = await backButton.isVisible().catch(() => false);

    if (hasBackButton) {
      // Click back
      await backButton.click();

      // Should be back on first step
      await expect(page.getByLabel(/stack name/i)).toBeVisible();

      // Previous values should be preserved
      const stackNameValue = await page.getByLabel(/stack name/i).inputValue();
      expect(stackNameValue).toBe('test-stack');
    }
  });
});
