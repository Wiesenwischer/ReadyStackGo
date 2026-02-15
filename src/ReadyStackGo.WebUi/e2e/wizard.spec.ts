import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Setup Wizard + Onboarding (v0.26)
 *
 * Flow: Create Admin (wizard) → mandatory onboarding (org → env → sources → done) → Dashboard
 *
 * IMPORTANT: Tests are ordered carefully. Pre-wizard tests (validation, UI checks)
 * must run BEFORE the complete flow test, because creating an admin permanently
 * marks the wizard as completed.
 */

test.describe('Setup Wizard - Pre-Setup Checks', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.clear();
    });
  });

  test('should redirect to wizard when not completed', async ({ page }) => {
    await page.goto('/');
    await page.waitForURL(/\/wizard/, { timeout: 5000 });

    await expect(page.getByRole('heading', { name: /Welcome to ReadyStackGo/i })).toBeVisible();
    await expect(page.getByText('Create your admin account to get started')).toBeVisible();
  });

  test('should show admin creation form with all fields', async ({ page }) => {
    await page.goto('/wizard');
    await page.waitForLoadState('networkidle');

    // Check heading and description
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();
    await expect(page.getByText('This will be the primary administrator account for ReadyStackGo')).toBeVisible();

    // Check form fields by placeholder
    await expect(page.getByPlaceholder('admin')).toBeVisible();
    await expect(page.getByPlaceholder('Enter a strong password')).toBeVisible();
    await expect(page.getByPlaceholder('Re-enter your password')).toBeVisible();

    // Check hints
    await expect(page.getByText('Minimum 3 characters')).toBeVisible();
    await expect(page.getByText('Minimum 8 characters')).toBeVisible();

    // Check submit button
    await expect(page.getByRole('button', { name: /Continue/i })).toBeVisible();

    // Screenshot: Wizard admin form
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-01-admin-form.png'),
      fullPage: false,
    });
  });

  test('should validate short username', async ({ page }) => {
    await page.goto('/wizard');
    await page.waitForLoadState('networkidle');

    // Disable HTML5 native validation so React validation fires
    await page.evaluate(() => {
      document.querySelector('form')?.setAttribute('novalidate', '');
    });

    await page.getByPlaceholder('admin').fill('ab');
    await page.getByPlaceholder('Enter a strong password').fill('ValidPassword123!');
    await page.getByPlaceholder('Re-enter your password').fill('ValidPassword123!');
    await page.getByRole('button', { name: /Continue/i }).click();

    await expect(page.getByText('Username must be at least 3 characters long')).toBeVisible({ timeout: 3000 });

    // Screenshot: Validation error
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-02-validation-error.png'),
      fullPage: false,
    });
  });

  test('should validate short password', async ({ page }) => {
    await page.goto('/wizard');
    await page.waitForLoadState('networkidle');

    // Disable HTML5 native validation so React validation fires
    await page.evaluate(() => {
      document.querySelector('form')?.setAttribute('novalidate', '');
    });

    await page.getByPlaceholder('admin').fill('testadmin');
    await page.getByPlaceholder('Enter a strong password').fill('short');
    await page.getByPlaceholder('Re-enter your password').fill('short');
    await page.getByRole('button', { name: /Continue/i }).click();

    await expect(page.getByText('Password must be at least 8 characters long')).toBeVisible({ timeout: 3000 });
  });

  test('should validate password mismatch', async ({ page }) => {
    await page.goto('/wizard');
    await page.waitForLoadState('networkidle');

    await page.getByPlaceholder('admin').fill('testadmin');
    await page.getByPlaceholder('Enter a strong password').fill('ValidPassword123!');
    await page.getByPlaceholder('Re-enter your password').fill('DifferentPassword456!');
    await page.getByRole('button', { name: /Continue/i }).click();

    await expect(page.getByText('Passwords do not match')).toBeVisible({ timeout: 3000 });
  });

  test('should toggle password visibility', async ({ page }) => {
    await page.goto('/wizard');
    await page.waitForLoadState('networkidle');

    const passwordInput = page.getByPlaceholder('Enter a strong password');
    await expect(passwordInput).toHaveAttribute('type', 'password');

    // Click the show/hide toggle button (eye icon)
    await page.locator('button[type="button"]').first().click();

    await expect(passwordInput).toHaveAttribute('type', 'text');
  });

  test('should have responsive design on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/wizard');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /Welcome to ReadyStackGo/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Create Admin Account/i })).toBeVisible();
    await expect(page.getByPlaceholder('admin')).toBeVisible();
  });
});

test.describe('Setup Wizard - Complete Flow with Onboarding', () => {
  test('should complete wizard and onboarding: admin → org → env → skip sources → dashboard', async ({ page }) => {
    // === WIZARD: Create Admin ===
    await page.goto('/wizard');
    await page.evaluate(() => { localStorage.clear(); });
    await page.waitForLoadState('networkidle');

    await page.getByPlaceholder('admin').fill('admin');
    await page.getByPlaceholder('Enter a strong password').fill('Admin1234');
    await page.getByPlaceholder('Re-enter your password').fill('Admin1234');
    await page.getByRole('button', { name: /Continue/i }).click();

    // Button should show loading state
    await expect(page.getByRole('button', { name: /Creating/i })).toBeVisible({ timeout: 2000 });

    // Should auto-login and redirect to /onboarding (OnboardingGuard intercepts /)
    await page.waitForURL(url => new URL(url).pathname === '/onboarding', { timeout: 15000 });
    await page.waitForLoadState('networkidle');

    // Screenshot: Onboarding start
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-03-onboarding-start.png'),
      fullPage: false,
    });

    // === ONBOARDING STEP 1: Organization (required, no skip) ===
    await expect(page.getByRole('heading', { name: 'Set Up ReadyStackGo' })).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Step 1 of 4')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Create Your Organization' })).toBeVisible();
    await expect(page.getByPlaceholder('My Company')).toBeVisible();

    // No "Skip for now" button on the org step
    await expect(page.getByRole('button', { name: /Skip/i })).not.toBeVisible();

    // Fill org name and submit
    await page.getByPlaceholder('My Company').fill('E2E Test Org');
    await page.getByRole('button', { name: /Continue/i }).click();

    // === ONBOARDING STEP 2: Docker Environment (skippable) ===
    await expect(page.getByText('Step 2 of 4')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('heading', { name: 'Add Docker Environment' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Skip for now/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Continue/i })).toBeVisible();

    // Screenshot: Environment step
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-04-onboarding-env.png'),
      fullPage: false,
    });

    // Fill environment form (don't skip — so we can reach Dashboard later)
    // Name is pre-filled with "Local Docker", socket path auto-populated from backend
    await expect(page.getByPlaceholder('Local Docker')).toHaveValue('Local Docker');
    await page.getByRole('button', { name: /Continue/i }).click();

    // === ONBOARDING STEP 3: Stack Sources (skippable) ===
    await expect(page.getByText('Step 3 of 4')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('heading', { name: 'Stack Sources' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Skip for now/i })).toBeVisible();

    // Screenshot: Sources step
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-05-onboarding-sources.png'),
      fullPage: false,
    });

    // Skip sources
    await page.getByRole('button', { name: /Skip for now/i }).click();

    // === ONBOARDING STEP 4: Complete ===
    await expect(page.getByText('Step 4 of 4')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('heading', { name: "You're All Set!" })).toBeVisible();

    // Verify summary: org ✓, env ✓, sources skipped
    await expect(page.getByText('Organization')).toBeVisible();
    await expect(page.getByText('Docker Environment')).toBeVisible();
    await expect(page.getByText('Stack Sources — skipped')).toBeVisible();

    // Screenshot: Onboarding complete
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-06-onboarding-complete.png'),
      fullPage: false,
    });

    // Click "Go to Dashboard"
    await page.getByRole('button', { name: /Go to Dashboard/i }).click();

    // Should reach the Dashboard (env was created, so EnvironmentGuard passes)
    // Multiple guards (OnboardingGuard + EnvironmentGuard) need to complete API calls first
    await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible({ timeout: 15000 });

    // Screenshot: Dashboard after onboarding
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-07-dashboard.png'),
      fullPage: false,
    });

    // === POST-ONBOARDING CHECKS ===

    // Accessing /onboarding after completion should redirect to dashboard
    await page.goto('/onboarding');
    await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible({ timeout: 15000 });

    // Accessing /wizard after completion should redirect away from wizard
    await page.goto('/wizard');
    await page.waitForURL(url => new URL(url).pathname !== '/wizard', { timeout: 10000 });
  });
});
