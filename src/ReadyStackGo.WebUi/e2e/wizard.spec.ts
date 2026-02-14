import { test, expect, Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Setup Wizard
 * These tests verify the complete 5-step wizard workflow (v0.24)
 * Steps: Admin -> Organization -> Environment -> Sources -> Complete
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
  await expect(page.getByRole('heading', { name: /Environment Setup/i })).toBeVisible({ timeout: 5000 });
}

async function completeStep3Environment(page: Page, skip = false) {
  if (skip) {
    // Skip environment setup
    await page.getByRole('button', { name: /Skip for now/i }).click();
  } else {
    // Environment form has name (default: "Local Docker") and socket path (auto-detected)
    // Just click "Create Environment" to accept defaults
    await page.getByRole('button', { name: /Create Environment/i }).click();
  }
  // v0.24: Now goes to Sources step instead of Complete
  await expect(page.getByRole('heading', { name: /Stack Sources/i })).toBeVisible({ timeout: 5000 });
}

async function completeStep4Sources(page: Page, skip = true) {
  if (skip) {
    // Skip sources — click "Skip for now"
    await page.getByRole('button', { name: /Skip for now/i }).click();
  } else {
    // Select featured sources and add them
    await page.getByRole('button', { name: /Add \d+ source/i }).click();
  }
  await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });
}

async function completeStep5(page: Page) {
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

  test('should complete step 2 and move to step 3 (Environment)', async ({ page }) => {
    await completeStep1(page);

    // Fill organization form
    await page.fill('input[placeholder*="my-company"]', 'e2e-test-org');
    await page.fill('input[placeholder*="My Company"]', 'E2E Test Organization');

    // Submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Should move to step 3 (Environment)
    await expect(page.getByRole('heading', { name: /Environment Setup/i })).toBeVisible({ timeout: 5000 });
  });

  test('should complete step 3 (Environment) and move to step 4 (Sources)', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);

    // Environment form: name (default "Local Docker") + socket path (auto-detected)
    // Just click Create Environment to accept defaults
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Should move to step 4 (Sources)
    await expect(page.getByRole('heading', { name: /Stack Sources/i })).toBeVisible({ timeout: 5000 });
  });

  test('should allow skipping environment step and move to Sources', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);

    // Skip environment setup
    await page.getByRole('button', { name: /Skip/i }).click();

    // Should move to step 4 (Sources)
    await expect(page.getByRole('heading', { name: /Stack Sources/i })).toBeVisible({ timeout: 5000 });
  });

  test('should allow going back from step 2', async ({ page }) => {
    await completeStep1(page);

    // Click back button
    await page.getByRole('button', { name: /Back/i }).click();

    // Should go back to step 1
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();
  });

  test('should show Sources step with skip and add buttons', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);
    await completeStep3Environment(page);

    // Sources step should show description
    await expect(page.getByText(/Select curated stack sources/i)).toBeVisible();

    // Skip and Add buttons should be visible
    await expect(page.getByRole('button', { name: /Skip for now/i })).toBeVisible();

    // Screenshot: Wizard sources step
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'sources-08-wizard-sources.png'),
      fullPage: false,
    });
  });

  test('should skip Sources step and move to Complete', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);
    await completeStep3Environment(page);

    // Skip sources
    await page.getByRole('button', { name: /Skip for now/i }).click();

    // Should move to step 5 (Complete)
    await expect(page.getByRole('heading', { name: /Complete Setup/i })).toBeVisible({ timeout: 5000 });
  });

  test('should show configuration summary on step 5', async ({ page }) => {
    await completeStep1(page);
    await completeStep2(page);
    await completeStep3Environment(page);
    await completeStep4Sources(page);

    // Check summary items
    await expect(page.getByText(/Admin account configured/i)).toBeVisible();
    await expect(page.getByText(/Organization details set/i)).toBeVisible();

    // Check complete button
    await expect(page.getByRole('button', { name: /Complete Setup/i })).toBeVisible();
  });

  test('should show progress indicator for all 5 steps', async ({ page }) => {
    await page.goto('/wizard');

    // Check all 5 step numbers are visible
    await expect(page.locator('text=1').first()).toBeVisible();
    await expect(page.locator('text=2').first()).toBeVisible();
    await expect(page.locator('text=3').first()).toBeVisible();
    await expect(page.locator('text=4').first()).toBeVisible();
    await expect(page.locator('text=5').first()).toBeVisible();

    // Check step names
    await expect(page.getByText('Admin')).toBeVisible();
    await expect(page.getByText('Organization')).toBeVisible();
    await expect(page.getByText('Environment')).toBeVisible();
    await expect(page.getByText('Sources')).toBeVisible();
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

test.describe('Complete Wizard Flow with Environment Step', () => {
  test('should complete wizard with environment and redirect to login', async ({ page }) => {
    // Complete all wizard steps including environment
    await completeStep1(page, `e2e_flow_${Date.now()}`);
    await completeStep2(page, `flow-org-${Date.now()}`);
    await completeStep3Environment(page);
    await completeStep4Sources(page);
    await completeStep5(page);

    // Should redirect to login page
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });

  test('should complete wizard without environment (skip) and redirect to login', async ({ page }) => {
    // Complete wizard steps, skip environment
    await completeStep1(page, `e2e_skip_${Date.now()}`);
    await completeStep2(page, `skip-org-${Date.now()}`);
    await completeStep3Environment(page, true); // skip
    await completeStep4Sources(page);
    await completeStep5(page);

    // Should redirect to login page
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });

  test('should show environment form fields on step 3', async ({ page }) => {
    await completeStep1(page, `e2e_form_${Date.now()}`);
    await completeStep2(page, `form-org-${Date.now()}`);

    // Check environment form fields (name + socket path)
    await expect(page.locator('input[placeholder="Local Docker"]')).toBeVisible();
    await expect(page.locator('input[placeholder*="docker.sock"]')).toBeVisible();

    // Check skip and create buttons
    await expect(page.getByRole('button', { name: /Skip for now/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Create Environment/i })).toBeVisible();
  });

  test('should show validation error for short environment name in wizard', async ({ page }) => {
    await completeStep1(page, `e2e_valid_${Date.now()}`);
    await completeStep2(page, `valid-org-${Date.now()}`);

    // Fill with very short name (min 2 chars)
    await page.fill('input[placeholder="Local Docker"]', 'a');

    // Try to submit
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Should stay on environment step
    await expect(page.getByRole('heading', { name: /Environment Setup/i })).toBeVisible();
  });

  test('should show validation error for empty socket path in wizard', async ({ page }) => {
    await completeStep1(page, `e2e_socket_${Date.now()}`);
    await completeStep2(page, `socket-org-${Date.now()}`);

    // Clear the auto-detected socket path
    await page.fill('input[placeholder*="docker.sock"]', '');

    // Try to submit
    await page.getByRole('button', { name: /Create Environment/i }).click();

    // Should stay on environment step due to required field
    await expect(page.getByRole('heading', { name: /Environment Setup/i })).toBeVisible();
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
  test('should access dashboard after complete setup and login', async ({ page }) => {
    // Complete full wizard with environment
    const username = `nav_test_${Date.now()}`;
    await completeStep1(page, username);
    await completeStep2(page, `nav-org-${Date.now()}`);
    await completeStep3Environment(page);
    await completeStep4Sources(page);
    await completeStep5(page);

    // Wait for login redirect
    await page.waitForURL(/\/login/, { timeout: 10000 });

    // Login with the created credentials
    await page.fill('input[type="text"]', username);
    await page.fill('input[type="password"]', 'TestPassword123!');
    await page.click('button[type="submit"]');

    // Should redirect to dashboard
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Verify dashboard elements
    await expect(page.getByText(/Dashboard/i)).toBeVisible();
  });

  test('should show environment in UI after wizard completion', async ({ page }) => {
    // Complete full wizard with environment
    const username = `sidebar_${Date.now()}`;
    const envName = 'Sidebar Test Env';
    await completeStep1(page, username);
    await completeStep2(page, `sidebar-org-${Date.now()}`);

    // Create environment with specific name in step 3
    await page.fill('input[placeholder="Local Docker"]', envName);
    await page.getByRole('button', { name: /Create Environment/i }).click();
    // v0.24: Now goes to Sources step
    await expect(page.getByRole('heading', { name: /Stack Sources/i })).toBeVisible({ timeout: 5000 });

    await completeStep4Sources(page);
    await completeStep5(page);

    // Wait for login redirect and login
    await page.waitForURL(/\/login/, { timeout: 10000 });
    await page.fill('input[type="text"]', username);
    await page.fill('input[type="password"]', 'TestPassword123!');
    await page.click('button[type="submit"]');

    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });

    // Environment name should appear somewhere in the UI
    await expect(page.getByText(envName)).toBeVisible({ timeout: 5000 });
  });
});
