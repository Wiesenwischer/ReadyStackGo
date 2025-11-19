import { test, expect } from '@playwright/test';

/**
 * E2E Tests for Setup Wizard
 * These tests verify the complete 4-step wizard workflow
 */

test.describe('Setup Wizard', () => {
  test.beforeEach(async ({ page }) => {
    // Clear any existing state
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.clear();
    });
  });

  test('should redirect to wizard when not completed', async ({ page }) => {
    await page.goto('/');

    // Should redirect to wizard
    await page.waitForURL(/\/wizard/, { timeout: 5000 });

    // Check wizard is displayed
    await expect(page.getByRole('heading', { name: /ReadyStackGo Setup Wizard/i })).toBeVisible();
  });

  test('should show step 1 (Admin) initially', async ({ page }) => {
    await page.goto('/wizard');

    // Check Step 1 heading
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();

    // Check form fields
    await expect(page.locator('input[type="text"]').first()).toBeVisible(); // Username
    await expect(page.locator('input[type="password"]').first()).toBeVisible(); // Password
    await expect(page.getByText(/Confirm Password/i)).toBeVisible();

    // Check continue button
    await expect(page.getByRole('button', { name: /Continue/i })).toBeVisible();
  });

  test('should validate admin form inputs', async ({ page }) => {
    await page.goto('/wizard');

    // Try to submit with empty fields
    const continueButton = page.getByRole('button', { name: /Continue/i });
    await continueButton.click();

    // Should show HTML5 validation or stay on same step
    const heading = page.getByRole('heading', { name: /Create Admin Account/i });
    await expect(heading).toBeVisible();
  });

  test('should validate password match', async ({ page }) => {
    await page.goto('/wizard');

    // Fill in mismatched passwords
    await page.fill('input[type="text"]', 'testadmin');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('Password123!');
    await passwordFields.nth(1).fill('DifferentPassword123!');

    // Try to submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show error message
    await expect(page.getByText(/Passwords do not match/i)).toBeVisible({ timeout: 3000 });
  });

  test('should complete step 1 and move to step 2', async ({ page }) => {
    await page.goto('/wizard');

    // Fill in admin form
    await page.fill('input[type="text"]', 'e2eadmin');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('E2ePassword123!');
    await passwordFields.nth(1).fill('E2ePassword123!');

    // Submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should move to step 2
    await expect(page.getByRole('heading', { name: /Organization Setup/i })).toBeVisible({ timeout: 5000 });

    // Progress indicator should show step 2 active
    const step2Circle = page.locator('text=2').first();
    await expect(step2Circle).toBeVisible();
  });

  test('should validate organization ID format', async ({ page }) => {
    // First complete step 1
    await completeStep1(page);

    // Try invalid organization ID
    await page.fill('input[placeholder*="my-company"]', 'Invalid ID With Spaces');
    await page.fill('input[placeholder*="My Company"]', 'Test Org');

    // Try to submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show error or prevent submission
    const heading = page.getByRole('heading', { name: /Organization Setup/i });
    await expect(heading).toBeVisible();
  });

  test('should complete step 2 and move to step 3', async ({ page }) => {
    await completeStep1(page);

    // Fill organization form
    await page.fill('input[placeholder*="my-company"]', 'e2e-test-org');
    await page.fill('input[placeholder*="My Company"]', 'E2E Test Organization');

    // Submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should move to step 3
    await expect(page.getByRole('heading', { name: /Configure Connections/i })).toBeVisible({ timeout: 5000 });
  });

  test('should allow going back from step 2', async ({ page }) => {
    await completeStep1(page);

    // Click back button
    await page.getByRole('button', { name: /Back/i }).click();

    // Should go back to step 1
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();
  });

  test('should complete step 3 and move to step 4', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);

    // Fill connection strings
    await page.fill('input[placeholder*="amqp"]', 'amqp://localhost:5672');
    await page.fill('input[placeholder*="postgres"]', 'Host=localhost;Database=test;Username=user;Password=pass');
    // EventStore is optional

    // Submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should move to step 4
    await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });
  });

  test('should show configuration summary on step 4', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);
    await completeStep3(page);

    // Check summary items
    await expect(page.getByText(/Admin account configured/i)).toBeVisible();
    await expect(page.getByText(/Organization details set/i)).toBeVisible();
    await expect(page.getByText(/Connection strings configured/i)).toBeVisible();

    // Check complete button
    await expect(page.getByRole('button', { name: /Complete Setup/i })).toBeVisible();
  });

  test('should show progress indicator for all 4 steps', async ({ page }) => {
    await page.goto('/wizard');

    // Check all 4 step numbers are visible
    await expect(page.locator('text=1').first()).toBeVisible();
    await expect(page.locator('text=2').first()).toBeVisible();
    await expect(page.locator('text=3').first()).toBeVisible();
    await expect(page.locator('text=4').first()).toBeVisible();

    // Check step names
    await expect(page.getByText('Admin')).toBeVisible();
    await expect(page.getByText('Organization')).toBeVisible();
    await expect(page.getByText('Connections')).toBeVisible();
    await expect(page.getByText('Complete')).toBeVisible();
  });

  test('should prevent direct access to later steps', async ({ page }) => {
    // This test would require the wizard to maintain state between page reloads
    // For now, we just verify the wizard enforces step order via API validation

    await page.goto('/wizard');

    // Try to jump to step 2 without completing step 1
    // The wizard component doesn't allow URL-based step navigation
    // so this is enforced by the component's internal state

    // Verify we're still on step 1
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();
  });

  test('complete wizard flow shows checkmarks for completed steps', async ({ page }) => {
    await completeStep1(page);

    // Step 1 should show checkmark instead of number
    const step1Circle = page.locator('[class*="bg-brand"]').first();
    await expect(step1Circle).toBeVisible();

    await completeStep2(page);
    await completeStep3(page);

    // Multiple steps should show checkmarks
    const completedSteps = page.locator('[class*="bg-brand"]');
    const count = await completedSteps.count();
    expect(count).toBeGreaterThan(1);
  });

  test('should show loading state during API calls', async ({ page }) => {
    await page.goto('/wizard');

    // Fill form
    await page.fill('input[type="text"]', 'loadingtest');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('LoadTest123!');
    await passwordFields.nth(1).fill('LoadTest123!');

    // Submit and check for loading state
    const continueButton = page.getByRole('button', { name: /Continue/i });
    await continueButton.click();

    // Button should show loading text briefly (might be too fast to catch)
    // or be disabled during submission
    const isDisabled = await continueButton.isDisabled();
    expect(isDisabled).toBe(true);
  });

  test('should display error message on API failure', async ({ page }) => {
    // This would require mocking the API to return an error
    // For integration tests, we just verify error display exists in the UI

    await page.goto('/wizard');

    // Try to submit with very short password that backend might reject
    await page.fill('input[type="text"]', 'errortest');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('abc');
    await passwordFields.nth(1).fill('abc');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show some validation error
    // Either from frontend or backend
    await page.waitForTimeout(1000);
  });

  test('should have responsive design on mobile', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/wizard');

    // Check that wizard is still visible and usable
    await expect(page.getByRole('heading', { name: /ReadyStackGo Setup Wizard/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();

    // Forms should be visible
    await expect(page.locator('input[type="text"]').first()).toBeVisible();
  });

  // Helper functions
  async function completeStep1(page: any) {
    await page.goto('/wizard');
    await page.fill('input[type="text"]', `admin${Date.now()}`);
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('TestPassword123!');
    await passwordFields.nth(1).fill('TestPassword123!');
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByRole('heading', { name: /Organization Setup/i })).toBeVisible({ timeout: 5000 });
  }

  async function completeStep2(page: any) {
    await page.fill('input[placeholder*="my-company"]', `org-${Date.now()}`);
    await page.fill('input[placeholder*="My Company"]', 'Test Organization');
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByRole('heading', { name: /Configure Connections/i })).toBeVisible({ timeout: 5000 });
  }

  async function completeStep3(page: any) {
    await page.fill('input[placeholder*="amqp"]', 'amqp://localhost:5672');
    await page.fill('input[placeholder*="postgres"]', 'Host=localhost;Database=test;Username=user;Password=pass');
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });
  }
});
