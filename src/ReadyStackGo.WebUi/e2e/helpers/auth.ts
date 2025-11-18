import { Page } from '@playwright/test';

/**
 * Helper function to log in a user via the UI
 * This simulates real user behavior and sets up authentication
 */
export async function login(page: Page, username: string = 'admin', password: string = 'admin') {
  // Navigate to login page
  await page.goto('/login');

  // Fill in credentials
  await page.fill('input[name="username"], input[type="text"]', username);
  await page.fill('input[name="password"], input[type="password"]', password);

  // Submit the form
  await page.click('button[type="submit"], button:has-text("Login"), button:has-text("Sign in")');

  // Wait for navigation to complete (should redirect to dashboard or home)
  await page.waitForURL(/\/(dashboard)?$/);

  // Wait for auth token to be stored
  await page.waitForFunction(() => {
    return localStorage.getItem('auth_token') !== null;
  });
}

/**
 * Helper function to check if user is logged in
 */
export async function isLoggedIn(page: Page): Promise<boolean> {
  return await page.evaluate(() => {
    return localStorage.getItem('auth_token') !== null;
  });
}

/**
 * Helper function to logout
 */
export async function logout(page: Page) {
  await page.evaluate(() => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
  });
}
