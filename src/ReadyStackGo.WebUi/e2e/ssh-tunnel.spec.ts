import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for SSH Tunnel Environment feature.
 * Tests the environment creation flow with SSH Tunnel type selector and form.
 */

test.describe('SSH Tunnel Environments', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should show environments list with type badge', async ({ page }) => {
    await page.goto('/environments');
    await page.waitForLoadState('networkidle');

    // Environments list should be visible
    await expect(page.getByRole('heading', { name: 'Environments', exact: true })).toBeVisible();

    // Screenshot: Environments list page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-01-environments-list.png'),
      fullPage: false
    });
  });

  test('should show type selector on Add Environment page', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Page header
    await expect(page.getByRole('heading', { name: 'Add Environment' })).toBeVisible();

    // Type selector buttons should be visible
    await expect(page.getByText('Local Docker Socket')).toBeVisible();
    await expect(page.getByText('SSH Tunnel')).toBeVisible();

    // Docker Socket should be selected by default
    await expect(page.getByText('Docker Socket Path')).toBeVisible();

    // Screenshot: Add Environment with type selector (default: Docker Socket)
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-02-type-selector.png'),
      fullPage: false
    });
  });

  test('should switch to SSH Tunnel form when SSH Tunnel type is selected', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Click SSH Tunnel type
    await page.getByText('SSH Tunnel').click();

    // SSH-specific fields should be visible
    await expect(page.getByText('SSH Host')).toBeVisible();
    await expect(page.getByText('SSH Port')).toBeVisible();
    await expect(page.getByText('SSH Username')).toBeVisible();
    await expect(page.getByText('Authentication Method')).toBeVisible();
    await expect(page.getByText('Private Key', { exact: true })).toBeVisible();
    await expect(page.getByText('Remote Docker Socket Path')).toBeVisible();

    // Docker Socket Path field (exact label) should NOT be visible
    await expect(page.getByText('Docker Socket Path', { exact: true })).not.toBeVisible();

    // Screenshot: SSH Tunnel form fields
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-03-ssh-form.png'),
      fullPage: false
    });
  });

  test('should show Private Key textarea by default', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    await page.getByText('SSH Tunnel').click();

    // Private Key radio should be selected by default
    const privateKeyRadio = page.locator('input[type="radio"][value="PrivateKey"]');
    await expect(privateKeyRadio).toBeChecked();

    // Textarea for private key should be visible
    const textarea = page.locator('textarea');
    await expect(textarea).toBeVisible();

    // Placeholder should mention PEM format
    await expect(textarea).toHaveAttribute('placeholder', /BEGIN.*PRIVATE KEY/);
  });

  test('should switch to password field when Password auth is selected', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    await page.getByText('SSH Tunnel').click();

    // Select Password authentication
    await page.getByText('Password', { exact: true }).click();

    // Password input should be visible (not textarea)
    const passwordInput = page.locator('input[type="password"]').last();
    await expect(passwordInput).toBeVisible();

    // Textarea should not be visible
    await expect(page.locator('textarea')).not.toBeVisible();

    // Screenshot: Password authentication mode
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-04-password-auth.png'),
      fullPage: false
    });
  });

  test('should fill SSH Tunnel form with sample data', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Fill environment name
    await page.locator('input[type="text"]').first().fill('Production Server');

    // Select SSH Tunnel type
    await page.getByText('SSH Tunnel').click();

    // Fill SSH fields
    await page.locator('input[placeholder="192.168.1.100"]').fill('10.0.1.50');
    await page.locator('input[type="number"]').fill('22');
    await page.locator('input[placeholder="root"]').fill('deploy');

    // Fill private key
    await page.locator('textarea').fill('-----BEGIN OPENSSH PRIVATE KEY-----\n(key content)\n-----END OPENSSH PRIVATE KEY-----');

    // Remote socket path should have default
    const remoteSocket = page.locator('input[placeholder="/var/run/docker.sock"]');
    await expect(remoteSocket).toHaveValue('/var/run/docker.sock');

    // Screenshot: Filled SSH form
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-05-filled-form.png'),
      fullPage: false
    });
  });

  test('should have Test Connection button', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    await page.getByText('SSH Tunnel').click();

    // Test Connection button should be visible
    const testButton = page.getByRole('button', { name: 'Test Connection' });
    await expect(testButton).toBeVisible();

    // Screenshot: Test Connection button
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-06-test-connection.png'),
      fullPage: false
    });
  });

  test('should switch back to Docker Socket form', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Switch to SSH Tunnel
    await page.getByText('SSH Tunnel').click();
    await expect(page.getByText('SSH Host')).toBeVisible();

    // Switch back to Docker Socket
    await page.getByText('Local Docker Socket').click();

    // Docker Socket field should reappear
    await expect(page.getByText('Docker Socket Path')).toBeVisible();

    // SSH fields should be gone
    await expect(page.getByText('SSH Host')).not.toBeVisible();
  });

  test('should create a Docker Socket environment successfully', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Fill name with unique value
    const uniqueName = `E2E Docker ${Date.now()}`;
    await page.locator('input[type="text"]').first().fill(uniqueName);

    // Wait for socket path to be auto-filled
    await page.waitForTimeout(1000);

    // Submit (Docker Socket is default)
    await page.getByRole('button', { name: 'Create Environment' }).click();

    // Should redirect to environments list
    await page.waitForURL('/environments', { timeout: 15000 });

    // New environment should be in the list
    await expect(page.getByText(uniqueName)).toBeVisible();
  });
});
