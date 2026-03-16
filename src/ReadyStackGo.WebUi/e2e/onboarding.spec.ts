import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Initial Setup: Wizard (Admin Creation) + Onboarding (Guided Setup)
 *
 * IMPORTANT: These tests require a FRESH container (no admin created yet).
 * Run: docker compose down -v && docker compose up -d
 *
 * The entire wizard + onboarding flow runs in a single test because:
 * - The wizard can only run once (admin creation)
 * - The onboarding is a continuous multi-step flow in one session
 * - Each step depends on the previous one completing
 */

test.describe('Initial Setup: Wizard & Onboarding', () => {
  test.setTimeout(120_000);

  test('complete wizard and onboarding flow', async ({ page }) => {
    // === WIZARD: Admin Creation ===

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Should redirect to /wizard
    await expect(page).toHaveURL(/\/wizard/);
    await expect(page.getByText('Create Admin Account')).toBeVisible();

    // Screenshot: Wizard admin creation page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-01-wizard-admin.png'),
      fullPage: false
    });

    // Fill and submit admin credentials
    await page.getByPlaceholder('admin').fill('admin');
    await page.getByPlaceholder('Enter a strong password').fill('Admin1234');
    await page.getByPlaceholder('Re-enter your password').fill('Admin1234');

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-02-wizard-filled.png'),
      fullPage: false
    });

    await page.getByRole('button', { name: 'Continue' }).click();

    // Should redirect to onboarding
    await page.waitForURL(/\/onboarding/, { timeout: 15_000 });

    // === ONBOARDING STEP 1: Organization ===

    await expect(page.getByText('Create Your Organization')).toBeVisible({ timeout: 10_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-03-organization.png'),
      fullPage: false
    });

    await page.getByPlaceholder('my-company').fill('my-company');
    await page.getByPlaceholder('My Company Inc.').fill('My Company');
    await page.getByRole('button', { name: 'Continue' }).click();

    // === ONBOARDING STEP 2: Docker Environment ===

    await expect(page.getByText('Add Docker Environment')).toBeVisible({ timeout: 10_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-04-environment.png'),
      fullPage: false
    });

    // Default values should be filled — just continue
    await page.getByRole('button', { name: 'Continue' }).click();

    // === ONBOARDING STEP 3: Stack Sources ===

    await expect(page.getByRole('heading', { name: 'Stack Sources' })).toBeVisible({ timeout: 10_000 });
    await page.waitForTimeout(1000); // Wait for sources to load

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-05-sources.png'),
      fullPage: false
    });

    // Add featured sources or skip
    const addButton = page.getByRole('button', { name: /add.*source/i });
    if (await addButton.isVisible().catch(() => false)) {
      await addButton.click();
    } else {
      await page.getByRole('button', { name: /skip/i }).click();
    }

    await page.waitForTimeout(2000);

    // === ONBOARDING STEP 4: Container Registries ===

    // Wait for registries step to appear and detect registries
    await page.waitForTimeout(5000);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-06-registries.png'),
      fullPage: false
    });

    // Try to skip or continue past registries
    const skipBtn = page.getByRole('button', { name: /skip for now/i });
    const continueBtn = page.getByRole('button', { name: /continue/i });
    if (await skipBtn.isVisible().catch(() => false)) {
      await skipBtn.click();
    } else if (await continueBtn.isVisible().catch(() => false)) {
      await continueBtn.click();
    }
    await page.waitForTimeout(3000);


    // === ONBOARDING STEP 5: Completion ===

    await expect(page.getByText("You're All Set!")).toBeVisible({ timeout: 15_000 });
    {
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'onboarding-07-complete.png'),
        fullPage: false
      });

      await page.getByRole('button', { name: 'Go to Dashboard' }).click();
    }

    // === DASHBOARD ===

    await page.waitForURL(/\/(dashboard)?$/, { timeout: 15_000 });
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Dashboard')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'onboarding-08-dashboard.png'),
      fullPage: false
    });
  });
});
