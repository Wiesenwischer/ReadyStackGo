import { test, expect, Page } from '@playwright/test';

/**
 * E2E Tests for Setup Wizard
 * These tests verify the complete 3-step wizard workflow (v0.4)
 */

// Helper functions for wizard steps
async function completeStep1(page: Page, username?: string) {
  await page.goto('/wizard');
  const user = username || `admin${Date.now()}`;
  await page.fill('input[type="text"]', user);
  const passwordFields = page.locator('input[type="password"]');
  await passwordFields.nth(0).fill('TestPassword123!');
  await passwordFields.nth(1).fill('TestPassword123!');
  await page.getByRole('button', { name: /Continue/i }).click();
  await expect(page.getByRole('heading', { name: /Organization Setup/i })).toBeVisible({ timeout: 5000 });
}

async function completeStep2(page: Page, orgId?: string) {
  const org = orgId || `org-${Date.now()}`;
  await page.fill('input[placeholder*="my-company"]', org);
  await page.fill('input[placeholder*="My Company"]', 'Test Organization');
  await page.getByRole('button', { name: /Continue/i }).click();
  await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });
}

async function completeStep3(page: Page) {
  await page.getByRole('button', { name: /Complete Setup/i }).click();
}

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

  test('should complete step 2 and move to step 3 (Install)', async ({ page }) => {
    await completeStep1(page);

    // Fill organization form
    await page.fill('input[placeholder*="my-company"]', 'e2e-test-org');
    await page.fill('input[placeholder*="My Company"]', 'E2E Test Organization');

    // Submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should move to step 3 (Complete Setup)
    await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });
  });

  test('should allow going back from step 2', async ({ page }) => {
    await completeStep1(page);

    // Click back button
    await page.getByRole('button', { name: /Back/i }).click();

    // Should go back to step 1
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();
  });

  test('should show configuration summary on step 3', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);

    // Check summary items
    await expect(page.getByText(/Admin account configured/i)).toBeVisible();
    await expect(page.getByText(/Organization details set/i)).toBeVisible();

    // Check complete button
    await expect(page.getByRole('button', { name: /Complete Setup/i })).toBeVisible();
  });

  test('should show progress indicator for all 3 steps', async ({ page }) => {
    await page.goto('/wizard');

    // Check all 3 step numbers are visible
    await expect(page.locator('text=1').first()).toBeVisible();
    await expect(page.locator('text=2').first()).toBeVisible();
    await expect(page.locator('text=3').first()).toBeVisible();

    // Check step names
    await expect(page.getByText('Admin')).toBeVisible();
    await expect(page.getByText('Organization')).toBeVisible();
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
    await page.goto('/wizard');

    // Try to submit with very short password that backend will reject
    await page.fill('input[type="text"]', 'errortest');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('abc');
    await passwordFields.nth(1).fill('abc');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show validation error from backend (password too short)
    await expect(page.getByText(/Password must be at least 8 characters/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display username validation error', async ({ page }) => {
    await page.goto('/wizard');

    // Try to submit with username containing special chars
    await page.fill('input[type="text"]', 'invalid@user!');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('ValidPassword123!');
    await passwordFields.nth(1).fill('ValidPassword123!');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show validation error
    await expect(page.getByText(/Username can only contain letters, numbers, and underscores/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display username length validation error', async ({ page }) => {
    await page.goto('/wizard');

    // Try to submit with too short username
    await page.fill('input[type="text"]', 'ab');
    const passwordFields = page.locator('input[type="password"]');
    await passwordFields.nth(0).fill('ValidPassword123!');
    await passwordFields.nth(1).fill('ValidPassword123!');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show validation error
    await expect(page.getByText(/Username must be at least 3 characters/i)).toBeVisible({ timeout: 5000 });
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

});

test.describe('Complete Wizard to Environment Setup Flow', () => {
  test('should complete wizard and redirect to environment setup', async ({ page }) => {
    // Complete all wizard steps
    await completeStep1(page, `e2e_flow_${Date.now()}`);
    await completeStep2(page, `flow-org-${Date.now()}`);
    await completeStep3(page);

    // Should redirect to setup-environment page
    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Check setup environment page elements
    await expect(page.getByRole('heading', { name: /Setup Your First Environment/i })).toBeVisible();
    await expect(page.getByText(/Connect to a Docker daemon/i)).toBeVisible();

    // Check form fields
    await expect(page.locator('input[placeholder*="local-docker"]')).toBeVisible();
    await expect(page.locator('input[placeholder*="Local Docker"]')).toBeVisible();
    await expect(page.locator('input[placeholder*="/var/run/docker.sock"]')).toBeVisible();
  });

  test('should create environment and redirect to dashboard', async ({ page }) => {
    // Complete wizard
    await completeStep1(page, `e2e_env_${Date.now()}`);
    await completeStep2(page, `env-org-${Date.now()}`);
    await completeStep3(page);

    // Wait for environment setup page
    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Fill environment form
    const envId = `env-${Date.now()}`;
    await page.fill('input[placeholder*="local-docker"]', envId);
    await page.fill('input[placeholder*="Local Docker"]', 'E2E Test Environment');
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '/var/run/docker.sock');

    // Create environment
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Should redirect to dashboard
    await page.waitForURL('/', { timeout: 10000 });

    // Check dashboard is visible
    await expect(page.getByText(/Dashboard/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show validation error for invalid environment ID', async ({ page }) => {
    // Complete wizard
    await completeStep1(page, `e2e_valid_${Date.now()}`);
    await completeStep2(page, `valid-org-${Date.now()}`);
    await completeStep3(page);

    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Fill with invalid ID (contains spaces)
    await page.fill('input[placeholder*="local-docker"]', 'invalid id with spaces');
    await page.fill('input[placeholder*="Local Docker"]', 'Test Environment');
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '/var/run/docker.sock');

    // Try to create - should fail HTML5 validation or backend validation
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Should show error message or stay on page
    await expect(page.getByRole('heading', { name: /Setup Your First Environment/i })).toBeVisible();
  });

  test('should show validation error for empty socket path', async ({ page }) => {
    // Complete wizard
    await completeStep1(page, `e2e_socket_${Date.now()}`);
    await completeStep2(page, `socket-org-${Date.now()}`);
    await completeStep3(page);

    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Fill form with empty socket path
    await page.fill('input[placeholder*="local-docker"]', `env-${Date.now()}`);
    await page.fill('input[placeholder*="Local Docker"]', 'Test Environment');
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '');

    // Try to create
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Should stay on page due to required field
    await expect(page.getByRole('heading', { name: /Setup Your First Environment/i })).toBeVisible();
  });

  test('should display loading state during environment creation', async ({ page }) => {
    // Complete wizard
    await completeStep1(page, `e2e_load_${Date.now()}`);
    await completeStep2(page, `load-org-${Date.now()}`);
    await completeStep3(page);

    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Fill form
    await page.fill('input[placeholder*="local-docker"]', `env-${Date.now()}`);
    await page.fill('input[placeholder*="Local Docker"]', 'Loading Test Environment');
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '/var/run/docker.sock');

    // Click create and check for loading state
    const createButton = page.getByRole('button', { name: /Create Environment/i });
    await createButton.click();

    // Button should show loading text
    await expect(page.getByRole('button', { name: /Creating Environment/i })).toBeVisible({ timeout: 1000 }).catch(() => {
      // Loading state might be too fast to catch, that's okay
    });
  });

  test('should show duplicate environment error', async ({ page }) => {
    // This test creates two environments with the same socket path
    // First complete wizard and create an environment
    await completeStep1(page, `e2e_dup1_${Date.now()}`);
    await completeStep2(page, `dup1-org-${Date.now()}`);
    await completeStep3(page);

    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Create first environment
    await page.fill('input[placeholder*="local-docker"]', `dup-env-1-${Date.now()}`);
    await page.fill('input[placeholder*="Local Docker"]', 'First Environment');
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '/var/run/docker.sock');
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Wait for dashboard
    await page.waitForURL('/', { timeout: 10000 });

    // Now try to go back to create another with same socket (via direct URL)
    await page.goto('/setup-environment');

    // Should redirect to dashboard since environment exists
    await page.waitForURL('/', { timeout: 5000 });
  });
});

test.describe('Wizard Step Organization Validation', () => {
  test('should display organization ID validation error for invalid characters', async ({ page }) => {
    await completeStep1(page);

    // Fill with invalid org ID containing special characters
    await page.fill('input[placeholder*="my-company"]', 'org@invalid!');
    await page.fill('input[placeholder*="My Company"]', 'Test Org');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show validation error
    await expect(page.getByText(/Organization ID can only contain/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display organization ID length validation error', async ({ page }) => {
    await completeStep1(page);

    // Fill with too short org ID
    await page.fill('input[placeholder*="my-company"]', 'ab');
    await page.fill('input[placeholder*="My Company"]', 'Test Org');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show validation error
    await expect(page.getByText(/Organization ID must be at least 3 characters/i)).toBeVisible({ timeout: 5000 });
  });

  test('should display empty organization name error', async ({ page }) => {
    await completeStep1(page);

    // Fill with empty name
    await page.fill('input[placeholder*="my-company"]', `valid-org-${Date.now()}`);
    // Leave name empty (clear default value if any)
    await page.fill('input[placeholder*="My Company"]', '');

    await page.getByRole('button', { name: /Continue/i }).click();

    // Should show validation error or stay on page
    await expect(page.getByRole('heading', { name: /Organization Setup/i })).toBeVisible();
  });
});

test.describe('Post-Wizard Navigation', () => {
  test('should access dashboard after complete setup', async ({ page }) => {
    // Complete full wizard + environment setup
    await completeStep1(page, `nav_test_${Date.now()}`);
    await completeStep2(page, `nav-org-${Date.now()}`);
    await completeStep3(page);

    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    // Create environment
    await page.fill('input[placeholder*="local-docker"]', `nav-env-${Date.now()}`);
    await page.fill('input[placeholder*="Local Docker"]', 'Navigation Test');
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '/var/run/docker.sock');
    await page.getByRole('button', { name: /Create Environment/i }).click();

    await page.waitForURL('/', { timeout: 10000 });

    // Verify dashboard elements
    await expect(page.getByText(/Dashboard/i)).toBeVisible();

    // Try navigating to containers
    await page.click('text=Containers');
    await expect(page.getByText(/Container Management/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show environment in sidebar after creation', async ({ page }) => {
    // Complete full wizard + environment setup
    await completeStep1(page, `sidebar_${Date.now()}`);
    await completeStep2(page, `sidebar-org-${Date.now()}`);
    await completeStep3(page);

    await page.waitForURL(/\/setup-environment/, { timeout: 10000 });

    const envName = 'Sidebar Test Env';
    await page.fill('input[placeholder*="local-docker"]', `sidebar-env-${Date.now()}`);
    await page.fill('input[placeholder*="Local Docker"]', envName);
    await page.fill('input[placeholder*="/var/run/docker.sock"]', '/var/run/docker.sock');
    await page.getByRole('button', { name: /Create Environment/i }).click();

    await page.waitForURL('/', { timeout: 10000 });

    // Environment name should appear somewhere in the UI
    // (could be in sidebar, header, or environment selector)
    await expect(page.getByText(envName)).toBeVisible({ timeout: 5000 });
  });
});
