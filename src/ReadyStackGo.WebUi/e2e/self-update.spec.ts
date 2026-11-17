import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Self-Update feature.
 * Uses API route mocking to simulate update availability and trigger responses,
 * since the actual Docker-based self-update cannot run in E2E test environments.
 *
 * The update flow:
 * 1. SidebarWidget shows update banner when a new version is available
 * 2. Clicking "Update now" navigates to a dedicated /update page
 * 3. The /update page triggers the API and shows progress/result
 *
 * Note: Route mocking must be set up BEFORE login, because the SidebarWidget
 * fetches version info immediately on mount (which happens after login redirect).
 */

const MOCK_VERSION_UPDATE = {
  serverVersion: '0.19.0',
  updateAvailable: true,
  latestVersion: '0.20.0',
  latestReleaseUrl: 'https://github.com/Wiesenwischer/ReadyStackGo/releases/tag/v0.20.0',
  build: { commitSha: 'abc1234', buildDate: '2026-01-01T00:00:00Z' },
};

const MOCK_VERSION_CURRENT = {
  serverVersion: '0.20.0',
  updateAvailable: false,
  build: { commitSha: 'abc1234', buildDate: '2026-01-01T00:00:00Z' },
};

async function loginWithMockedVersion(page: Page, versionResponse: object) {
  // Set up route interception BEFORE navigating (so the sidebar gets mocked data)
  await page.route('**/api/system/version', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(versionResponse),
    });
  });

  // Clear any previously dismissed version
  await page.goto('/login');
  await page.evaluate(() => localStorage.removeItem('rsgo_update_dismissed'));

  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'Admin1234');
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  await page.waitForLoadState('networkidle');
}

test.describe('Self-Update', () => {
  test('should show update banner when a new version is available', async ({ page }) => {
    await loginWithMockedVersion(page, MOCK_VERSION_UPDATE);

    // Update banner should be visible in the sidebar
    await expect(page.getByText('v0.20.0')).toBeVisible();
    await expect(page.getByText('Update available')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Update now' })).toBeVisible();
    await expect(page.getByRole('link', { name: "See what's new" })).toBeVisible();

    // Scroll sidebar to ensure widget is visible, then take focused screenshot
    const sidebar = page.locator('aside').first();
    const widget = sidebar.locator('text=Update available').first();
    await widget.scrollIntoViewIfNeeded();
    await page.waitForTimeout(300);

    // Screenshot: Sidebar with update banner
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'self-update-01-update-banner.png'),
      fullPage: false,
    });
  });

  test('should navigate to update page and show updating state', async ({ page }) => {
    // Mock update trigger API to return success
    await page.route('**/api/system/update', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Update to v0.20.0 initiated. RSGO will restart momentarily.',
        }),
      });
    });

    await loginWithMockedVersion(page, MOCK_VERSION_UPDATE);

    // Click the update button — should navigate to /update page
    await page.getByRole('button', { name: 'Update now' }).click();
    await page.waitForURL(/\/update\?/, { timeout: 5000 });

    // Should show the dedicated update page with spinner
    await expect(page.getByText('Updating to v0.20.0')).toBeVisible();
    await expect(page.getByText('RSGO will restart momentarily.')).toBeVisible();

    // Version badge showing transition
    await expect(page.getByText('v0.19.0')).toBeVisible();

    // Screenshot: Dedicated update page with progress
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'self-update-02-updating.png'),
      fullPage: false,
    });
  });

  test('should show error state on update page when update fails', async ({ page }) => {
    // Mock update trigger API to return failure
    await page.route('**/api/system/update', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          message: 'Docker error: unable to pull image',
        }),
      });
    });

    await loginWithMockedVersion(page, MOCK_VERSION_UPDATE);

    await page.getByRole('button', { name: 'Update now' }).click();
    await page.waitForURL(/\/update\?/, { timeout: 5000 });

    // Should show error state on update page
    await expect(page.getByText('Update failed')).toBeVisible();
    await expect(page.getByText('Docker error: unable to pull image')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Retry update' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Back to Dashboard' })).toBeVisible();

    // Screenshot: Error state on update page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'self-update-03-error.png'),
      fullPage: false,
    });
  });

  test('should show the real failure reason behind a Details button when the pull fails', async ({ page }) => {
    const details =
      'Image: wiesenwischer/readystackgo:0.20.0\nDocker reported: write /var/lib/docker/tmp/x: no space left on device';

    // The update page talks to the anonymous UpdateHub over SignalR (JSON protocol).
    // Mock negotiate + WebSocket so the hub reports the failed pull as its current progress.
    await page.route('**/hubs/update/negotiate*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          negotiateVersion: 1,
          connectionId: 'e2e-conn',
          connectionToken: 'e2e-token',
          availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text'] }],
        }),
      });
    });

    // Like the real hub: report the current progress (idle) on connect, push the error once the update is triggered.
    const RS = String.fromCharCode(0x1e); // SignalR record separator
    const progressFrame = (payload: object) =>
      JSON.stringify({ type: 1, target: 'UpdateProgress', arguments: [payload] }) + RS;
    let pushToClient: ((payload: object) => void) | null = null;

    await page.routeWebSocket(/\/hubs\/update/, (ws) => {
      pushToClient = (payload) => ws.send(progressFrame(payload));
      ws.onMessage((message) => {
        if (typeof message === 'string' && message.includes('"protocol"')) {
          ws.send(`{}${RS}`);
          ws.send(progressFrame({ phase: 'idle' }));
        }
      });
    });

    await page.route('**/api/system/version', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_VERSION_UPDATE) });
    });

    // The page re-triggers the update when the hub reports an error: never let that reach a real server.
    await page.route('**/api/system/update', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, message: 'Update to v0.20.0 initiated.' }),
      });
      // The background update then fails while pulling the image.
      pushToClient?.({
        phase: 'error',
        message: 'Could not download wiesenwischer/readystackgo:0.20.0. The previous version is still running.',
        details,
      });
    });

    // Signed-in state without a backend: the auth provider only reads a non-expired JWT from storage.
    await page.route('**/api/wizard/status', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ wizardState: 'Installed', isCompleted: true, defaultDockerSocketPath: 'unix:///var/run/docker.sock' }),
      });
    });
    await page.addInitScript(() => {
      const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, '');
      const token = `${b64({ alg: 'none', typ: 'JWT' })}.${b64({ exp: Math.floor(Date.now() / 1000) + 3600 })}.sig`;
      localStorage.setItem('auth_token', token);
      localStorage.setItem('auth_user', JSON.stringify({ username: 'admin', role: 'admin' }));
    });

    await page.goto('/update?version=0.20.0');

    await expect(page.getByText('Update failed')).toBeVisible();
    await expect(page.getByText('Could not download wiesenwischer/readystackgo:0.20.0.')).toBeVisible();

    // Details are hidden until requested
    await expect(page.getByText('no space left on device')).toHaveCount(0);

    await page.getByRole('button', { name: 'Show details' }).click();
    await expect(page.getByText('no space left on device')).toBeVisible();

    await page.getByRole('button', { name: 'Hide details' }).click();
    await expect(page.getByText('no space left on device')).toHaveCount(0);
  });

  test('should dismiss update banner', async ({ page }) => {
    await loginWithMockedVersion(page, MOCK_VERSION_UPDATE);

    // Banner should be visible
    await expect(page.getByText('Update available')).toBeVisible();

    // Dismiss the banner (X button)
    await page.getByRole('button', { name: 'Dismiss' }).click();

    // Banner should disappear
    await expect(page.getByText('Update available')).not.toBeVisible();

    // Should persist after reload
    await page.reload();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Update available')).not.toBeVisible();
  });

  test('should not show update banner when no update is available', async ({ page }) => {
    await loginWithMockedVersion(page, MOCK_VERSION_CURRENT);

    // No update banner should be shown
    await expect(page.getByText('Update available')).not.toBeVisible();

    // Version should still display in widget
    await expect(page.getByText('v0.20.0')).toBeVisible();
  });

  test('should pass release URL to update page', async ({ page }) => {
    // Mock update trigger API
    await page.route('**/api/system/update', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, message: 'Update initiated.' }),
      });
    });

    await loginWithMockedVersion(page, MOCK_VERSION_UPDATE);

    await page.getByRole('button', { name: 'Update now' }).click();
    await page.waitForURL(/\/update\?/, { timeout: 5000 });

    // The "See what's new" link should be visible on the update page
    const releaseLink = page.getByRole('link', { name: "See what's new" });
    await expect(releaseLink).toBeVisible();
    await expect(releaseLink).toHaveAttribute(
      'href',
      'https://github.com/Wiesenwischer/ReadyStackGo/releases/tag/v0.20.0'
    );
  });
});
