import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

/**
 * E2E Tests for Environment Type Selector and SSH Tunnel Environment.
 * Tests the two-step environment creation flow: type selection → type-specific form.
 */

test.describe('Environment Type Selector & SSH Tunnel', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should show environments list', async ({ page }) => {
    await page.goto('/environments');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Environments', exact: true })).toBeVisible();

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
    await expect(page.getByText('Choose how to connect to a Docker daemon')).toBeVisible();

    // Type options should be visible
    await expect(page.getByText('Local Docker Socket')).toBeVisible();
    await expect(page.getByText('SSH Tunnel')).toBeVisible();

    // Continue button should be disabled (no selection yet)
    await expect(page.getByRole('button', { name: 'Continue' })).toBeDisabled();

    // Screenshot: Type selector page
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-02-type-selector.png'),
      fullPage: false
    });
  });

  test('should navigate to Docker Socket form when selected', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Select Docker Socket
    await page.getByText('Local Docker Socket').click();

    // Continue button should be enabled
    await expect(page.getByRole('button', { name: 'Continue' })).toBeEnabled();

    // Click Continue
    await page.getByRole('button', { name: 'Continue' }).click();

    // Should navigate to Docker Socket form
    await page.waitForURL('/environments/add/docker-socket');

    // Form fields should be visible
    await expect(page.getByRole('heading', { name: 'Local Docker Socket' })).toBeVisible();
    await expect(page.getByText('Environment Name')).toBeVisible();
    await expect(page.getByText('Docker Socket Path')).toBeVisible();

    // Back button should go to type selector
    await expect(page.getByRole('link', { name: 'Back' })).toBeVisible();
  });

  test('should navigate to SSH Tunnel form when selected', async ({ page }) => {
    await page.goto('/environments/add');
    await page.waitForLoadState('networkidle');

    // Select SSH Tunnel
    await page.getByText('SSH Tunnel').click();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Should navigate to SSH Tunnel form
    await page.waitForURL('/environments/add/ssh-tunnel');

    // SSH-specific fields should be visible
    await expect(page.getByRole('heading', { name: 'SSH Tunnel Environment' })).toBeVisible();
    await expect(page.getByText('SSH Host')).toBeVisible();
    await expect(page.getByText('SSH Port')).toBeVisible();
    await expect(page.getByText('SSH Username')).toBeVisible();
    await expect(page.getByText('Authentication Method')).toBeVisible();

    // Screenshot: SSH Tunnel form
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-03-ssh-form.png'),
      fullPage: false
    });
  });

  test('should show Private Key textarea by default', async ({ page }) => {
    await page.goto('/environments/add/ssh-tunnel');
    await page.waitForLoadState('networkidle');

    // Private Key radio should be selected by default
    const privateKeyRadio = page.locator('input[type="radio"][value="PrivateKey"]');
    await expect(privateKeyRadio).toBeChecked();

    // Textarea for private key should be visible
    const textarea = page.locator('textarea');
    await expect(textarea).toBeVisible();
    await expect(textarea).toHaveAttribute('placeholder', /BEGIN.*PRIVATE KEY/);
  });

  test('should switch to password field when Password auth is selected', async ({ page }) => {
    await page.goto('/environments/add/ssh-tunnel');
    await page.waitForLoadState('networkidle');

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
    await page.goto('/environments/add/ssh-tunnel');
    await page.waitForLoadState('networkidle');

    // Fill environment name
    await page.locator('input[type="text"]').first().fill('Production Server');

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

  test('should have Test Connection button on SSH form', async ({ page }) => {
    await page.goto('/environments/add/ssh-tunnel');
    await page.waitForLoadState('networkidle');

    const testButton = page.getByRole('button', { name: 'Test Connection' });
    await expect(testButton).toBeVisible();

    // Screenshot: Test Connection button
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'ssh-tunnel-06-test-connection.png'),
      fullPage: false
    });
  });

  test('should navigate back from SSH form to type selector', async ({ page }) => {
    await page.goto('/environments/add/ssh-tunnel');
    await page.waitForLoadState('networkidle');

    // Click Back
    await page.getByRole('link', { name: 'Back' }).click();

    // Should be back on type selector
    await page.waitForURL('/environments/add');
    await expect(page.getByText('Choose how to connect to a Docker daemon')).toBeVisible();
  });

  test('should create a Docker Socket environment successfully', async ({ page }) => {
    await page.goto('/environments/add/docker-socket');
    await page.waitForLoadState('networkidle');

    // Fill name with unique value
    const uniqueName = `E2E Docker ${Date.now()}`;
    await page.locator('input[type="text"]').first().fill(uniqueName);

    // Wait for socket path to be auto-filled
    await page.waitForTimeout(1000);

    // Submit
    await page.getByRole('button', { name: 'Create Environment' }).click();

    // Should redirect to environments list
    await page.waitForURL('/environments', { timeout: 15000 });

    // New environment should be in the list
    await expect(page.getByText(uniqueName)).toBeVisible();
  });
});
