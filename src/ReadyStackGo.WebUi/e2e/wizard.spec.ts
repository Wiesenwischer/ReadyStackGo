import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Setup Wizard (v0.26)
 * The wizard is a single-step admin creation flow:
 *   Create Admin → auto-login → redirect to dashboard
 * A 5-minute countdown timer is shown. After timeout/lock, the wizard is inaccessible.
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

test.describe('Setup Wizard - Complete Flow', () => {
  test('should create admin, auto-login, and redirect to app', async ({ page }) => {
    await page.goto('/wizard');
    await page.evaluate(() => { localStorage.clear(); });
    await page.waitForLoadState('networkidle');

    // Fill admin form
    await page.getByPlaceholder('admin').fill('admin');
    await page.getByPlaceholder('Enter a strong password').fill('Admin1234');
    await page.getByPlaceholder('Re-enter your password').fill('Admin1234');

    // Submit
    await page.getByRole('button', { name: /Continue/i }).click();

    // Button should show loading state and be disabled
    await expect(page.getByRole('button', { name: /Creating/i })).toBeVisible({ timeout: 2000 });

    // Should auto-login and redirect to the app (not login page)
    // EnvironmentGuard redirects to /environments when no environments exist
    await page.waitForURL(/\/environments/, { timeout: 15000 });
    await page.waitForLoadState('networkidle');

    // Verify we're logged in (admin username visible in top-right user menu)
    await expect(page.getByRole('button', { name: 'User menu' })).toBeVisible({ timeout: 5000 });

    // Screenshot: App after wizard completion
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'wizard-03-after-wizard.png'),
      fullPage: false,
    });

    // Try to access wizard again — should redirect to app (not wizard)
    await page.goto('/wizard');
    await page.waitForURL(/\/environments/, { timeout: 10000 });
  });
});
