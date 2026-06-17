import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

async function login(page: Page) {
  await page.goto('/login');
  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'Admin1234');
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard)?$/, { timeout: 15000 });
}

test.describe('Email & OIDC authentication', () => {
  test('login page shows email-or-username and SSO provider', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');
    // SSO button appears because an OIDC provider is configured + enabled.
    await expect(page.getByRole('button', { name: /Sign in with IdentityAccess/i })).toBeVisible();
    await expect(page.getByText('Email or username')).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-01-login.png'), fullPage: false });
  });

  test('settings overview shows invitations, email and SSO cards', async ({ page }) => {
    await login(page);
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('User Invitations', { exact: true })).toBeVisible();
    await expect(page.getByText('Email (SMTP)', { exact: true })).toBeVisible();
    await expect(page.getByText('Single Sign-On (OIDC)', { exact: true })).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-02-settings.png'), fullPage: false });
  });

  test('SMTP settings page', async ({ page }) => {
    await login(page);
    await page.goto('/settings/email');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Email (SMTP)', { exact: true })).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-03-smtp-settings.png'), fullPage: false });
  });

  test('OIDC settings page', async ({ page }) => {
    await login(page);
    await page.goto('/settings/oidc');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Single Sign-On (OIDC)', { exact: true })).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-04-oidc-settings.png'), fullPage: false });
  });

  test('invitations page with pending invitation', async ({ page }) => {
    await login(page);
    await page.goto('/settings/invitations');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Invite a user', { exact: true })).toBeVisible();
    // The invitation seeded via the API is listed.
    await expect(page.getByText('invitee@example.com')).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-05-invitations.png'), fullPage: false });
  });

  test('accept invitation page', async ({ page }) => {
    // Stub the invitation lookup so the acceptance form renders (the real token only
    // exists in the invitation email).
    await page.route('**/api/auth/invitation*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ valid: true, email: 'invitee@example.com', roleId: 'Operator', scopeType: 'Organization' }),
      }),
    );
    await page.goto('/accept-invite?token=demo-token');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Accept invitation', { exact: true })).toBeVisible();
    await expect(page.getByText('invitee@example.com')).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-06-accept-invite.png'), fullPage: false });
  });

  test('email-not-verified banner on dashboard', async ({ page }) => {
    await login(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    // The bootstrap admin is unverified and SMTP is configured, so the banner shows.
    await expect(page.getByText(/email address is not verified/i)).toBeVisible();
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'auth-07-verify-banner.png'), fullPage: false });
  });
});
