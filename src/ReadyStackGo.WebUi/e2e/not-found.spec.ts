import { test, expect } from '@playwright/test';

/**
 * E2E Tests for 404 Not Found Page
 * These tests verify that non-existent routes show the 404 page
 */

test.describe('404 Not Found Page', () => {
  test.beforeEach(async ({ page }) => {
    // Login first since protected routes require auth
    await page.goto('/login');
    await page.evaluate(() => localStorage.clear());
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 5000 });
  });

  test('should show 404 page for non-existent route', async ({ page }) => {
    await page.goto('/this-page-does-not-exist');

    // Should show ERROR heading
    await expect(page.getByRole('heading', { name: /error/i })).toBeVisible();

    // Should show 404 image (use first() since both light/dark images exist in DOM)
    await expect(page.locator('img[alt="404"]').first()).toBeVisible();

    // Should show helpful message
    await expect(page.getByText(/can't seem to find the page/i)).toBeVisible();

    // Should have back to home link
    await expect(page.getByRole('link', { name: /back to home/i })).toBeVisible();
  });

  test('should show 404 page for old settings/environments route', async ({ page }) => {
    // This was the old broken route before the fix
    await page.goto('/settings/environments');

    // Should show 404 page since this route doesn't exist
    await expect(page.getByRole('heading', { name: /error/i })).toBeVisible();
    await expect(page.getByText(/can't seem to find the page/i)).toBeVisible();
  });

  test('should show 404 page for deeply nested non-existent route', async ({ page }) => {
    await page.goto('/some/deep/nested/route/that/does/not/exist');

    // Should show 404 page
    await expect(page.getByRole('heading', { name: /error/i })).toBeVisible();
    await expect(page.locator('img[alt="404"]').first()).toBeVisible();
  });

  test('should navigate back to home from 404 page', async ({ page }) => {
    await page.goto('/nonexistent-page');

    // Click back to home link
    await page.click('a:has-text("Back to Home")');

    // Should navigate to dashboard/home
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 5000 });
  });

  test('should have both light and dark mode 404 images in DOM', async ({ page }) => {
    await page.goto('/nonexistent-page');

    // Both images should exist in the DOM (visibility controlled by CSS)
    const lightImage = page.locator('img[src*="404.svg"]:not([src*="dark"])');
    const darkImage = page.locator('img[src*="404-dark.svg"]');

    // Both should be present in the DOM
    await expect(lightImage).toHaveCount(1);
    await expect(darkImage).toHaveCount(1);
  });

  test('should show ReadyStackGo copyright in footer', async ({ page }) => {
    await page.goto('/nonexistent-page');

    // Should show copyright with current year and ReadyStackGo
    const currentYear = new Date().getFullYear();
    await expect(page.getByText(new RegExp(`${currentYear}.*ReadyStackGo`, 'i'))).toBeVisible();
  });
});

test.describe('404 Not Found Page - Unauthenticated', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.evaluate(() => localStorage.clear());
  });

  test('should show 404 page when accessing non-existent route without auth', async ({ page }) => {
    // The 404 catch-all route is outside ProtectedRoute, so it shows 404 directly
    await page.goto('/this-page-does-not-exist');

    // Should show 404 page (not redirect to login)
    await expect(page.getByRole('heading', { name: /error/i })).toBeVisible();
    await expect(page.getByText(/can't seem to find the page/i)).toBeVisible();
  });
});
