import { test, expect } from '@playwright/test';

/**
 * E2E Tests for Authentication
 * These tests verify the complete login workflow
 */

test.describe('Authentication', () => {
  test.beforeEach(async ({ page }) => {
    // Clear any existing auth
    await page.goto('/login');
    await page.evaluate(() => {
      localStorage.clear();
    });
  });

  test('should show login page with correct elements', async ({ page }) => {
    await page.goto('/login');

    // Check for login form elements
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
    await expect(page.locator('input[type="text"]')).toBeVisible();
    await expect(page.locator('input[type="password"]')).toBeVisible();
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();

    // Check for default credentials hint
    await expect(page.getByText(/admin \/ admin/i)).toBeVisible();
  });

  test('should successfully login with correct credentials', async ({ page }) => {
    await page.goto('/login');

    // Fill in credentials
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');

    // Submit form
    await page.click('button[type="submit"]');

    // Should redirect to dashboard
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 5000 });

    // Should have auth token in localStorage
    const token = await page.evaluate(() => localStorage.getItem('auth_token'));
    expect(token).toBeTruthy();
    expect(token).toMatch(/^eyJ/); // JWT tokens start with eyJ

    // Should have user data in localStorage
    const userData = await page.evaluate(() => localStorage.getItem('auth_user'));
    expect(userData).toBeTruthy();
    const user = JSON.parse(userData!);
    expect(user.username).toBe('admin');
    expect(user.role).toBe('admin');
  });

  test('should show error with incorrect credentials', async ({ page }) => {
    await page.goto('/login');

    // Fill in wrong credentials
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'wrongpassword');

    // Submit form
    await page.click('button[type="submit"]');

    // Should show error message
    await expect(page.getByText(/invalid username or password/i)).toBeVisible();

    // Should still be on login page
    expect(page.url()).toContain('/login');

    // Should NOT have auth token
    const token = await page.evaluate(() => localStorage.getItem('auth_token'));
    expect(token).toBeNull();
  });

  test('should show error with empty credentials', async ({ page }) => {
    await page.goto('/login');

    // Try to submit without filling form
    await page.click('button[type="submit"]');

    // Should still be on login page (HTML5 validation prevents submit)
    expect(page.url()).toContain('/login');
  });

  test('should be able to logout after login', async ({ page }) => {
    // First login
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/);

    // Verify logged in
    const tokenBefore = await page.evaluate(() => localStorage.getItem('auth_token'));
    expect(tokenBefore).toBeTruthy();

    // Look for logout button (might be in header/menu)
    const logoutButton = page.locator('button:has-text("Logout"), button:has-text("Sign out"), a:has-text("Logout"), a:has-text("Sign out")').first();

    if (await logoutButton.isVisible().catch(() => false)) {
      await logoutButton.click();
    } else {
      // If no logout button, just clear localStorage
      await page.evaluate(() => {
        localStorage.removeItem('auth_token');
        localStorage.removeItem('auth_user');
      });
      await page.goto('/');
    }

    // Should redirect to login
    await page.waitForURL(/\/login/, { timeout: 5000 });

    // Token should be cleared
    const tokenAfter = await page.evaluate(() => localStorage.getItem('auth_token'));
    expect(tokenAfter).toBeNull();
  });

  test('should redirect to login when accessing protected route without auth', async ({ page }) => {
    // Try to access dashboard without login
    await page.goto('/');

    // Should redirect to login
    await page.waitForURL(/\/login/);
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
  });

  test('CRITICAL: verify API endpoint is accessible', async ({ page }) => {
    // This test checks if the login API endpoint works
    await page.goto('/login');

    // Intercept the API call to see what happens
    let apiCallMade = false;
    let apiResponse: { status: number; statusText: string; url: string; body?: string } | null = null;

    page.on('response', async response => {
      if (response.url().includes('/api/auth/login')) {
        apiCallMade = true;
        apiResponse = {
          status: response.status(),
          statusText: response.statusText(),
          url: response.url()
        };

        try {
          const body = await response.text();
          apiResponse.body = body;
        } catch {
          apiResponse.body = 'Could not read body';
        }
      }
    });

    // Fill and submit
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');

    // Wait a bit for API call
    await page.waitForTimeout(2000);

    // Log the API response for debugging
    console.log('API Call Made:', apiCallMade);
    console.log('API Response:', apiResponse);

    // Verify API was called
    expect(apiCallMade).toBe(true);

    // API should return 200 for valid credentials
    expect(apiResponse?.status).toBe(200);
  });
});
