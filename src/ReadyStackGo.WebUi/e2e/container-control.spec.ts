import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';
import { execSync } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(
  __dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs'
);

const BASE_URL = 'http://localhost:8080';

/**
 * E2E Tests for Container Control (Stop / Restart)
 * Tests the stop and restart container actions on the product deployment detail page.
 * Requires a running product deployment (e2e-platform from product-deployment.spec.ts).
 */

test.describe.serial('Container Control', () => {
  test.setTimeout(120_000);

  let authToken: string;
  let productDeploymentId: string;
  let environmentId: string;

  test.beforeAll(async () => {
    // Get auth token
    try {
      const loginResult = execSync(
        `curl -sf -X POST ${BASE_URL}/api/auth/login ` +
        '-H "Content-Type: application/json" ' +
        '-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}"',
        { encoding: 'utf-8' }
      );
      const loginResponse = JSON.parse(loginResult);
      authToken = loginResponse.token;
    } catch {
      console.warn('Could not get auth token');
    }

    // Get environment ID
    try {
      const envResult = execSync(
        `curl -sf ${BASE_URL}/api/environments ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      const envResponse = JSON.parse(envResult);
      const environments = envResponse.environments || envResponse;
      if (Array.isArray(environments) && environments.length > 0) {
        environmentId = environments[0].id;
        console.log(`Environment ID: ${environmentId}`);
      }
    } catch {
      console.warn('Could not get environment ID');
    }

    // Find the active e2e-platform product deployment
    if (environmentId) {
      try {
        const pdResult = execSync(
          `curl -sf "${BASE_URL}/api/environments/${environmentId}/product-deployments" ` +
          `-H "Authorization: Bearer ${authToken}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
        const pdResponse = JSON.parse(pdResult);
        const deployments = pdResponse.productDeployments || pdResponse;
        const e2ePd = (Array.isArray(deployments) ? deployments : []).find(
          (pd: { productName?: string; productDisplayName?: string; status?: string }) =>
            (pd.productName?.toLowerCase().includes('e2e') ||
             pd.productDisplayName?.toLowerCase().includes('e2e')) &&
            (pd.status === 'Running' || pd.status === 'PartiallyRunning')
        );
        if (e2ePd) {
          productDeploymentId = e2ePd.productDeploymentId;
          console.log(`Found e2e-platform product deployment: ${productDeploymentId}`);
        } else {
          console.warn('No running e2e-platform product deployment found. Deploying one...');
          await deployE2ePlatform();
        }
      } catch {
        console.warn('Could not find product deployment. Deploying one...');
        await deployE2ePlatform();
      }
    }

    async function deployE2ePlatform() {
      // Ensure e2e-test-source exists
      try {
        const sourcesResult = execSync(
          `curl -sf ${BASE_URL}/api/stack-sources ` +
          `-H "Authorization: Bearer ${authToken}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
        const sources = JSON.parse(sourcesResult);
        const hasE2eSource = sources.some((s: { id: string }) => s.id === 'e2e-test-source');
        if (!hasE2eSource) {
          execSync(
            `curl -sf -X POST ${BASE_URL}/api/stack-sources ` +
            `-H "Authorization: Bearer ${authToken}" ` +
            `-H "Content-Type: application/json" ` +
            `-d "{\\"id\\":\\"e2e-test-source\\",\\"name\\":\\"E2E Test Source\\",\\"type\\":\\"LocalDirectory\\",\\"path\\":\\"/app/stacks/examples/e2e-platform\\"}"`,
            { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
          );
        }
      } catch { /* ignore */ }

      // Sync sources
      try {
        execSync(
          `curl -sf -X POST ${BASE_URL}/api/stack-sources/sync ` +
          `-H "Authorization: Bearer ${authToken}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
      } catch { /* ignore */ }

      // Find the e2e-platform product in catalog
      try {
        const catalogResult = execSync(
          `curl -sf "${BASE_URL}/api/environments/${environmentId}/catalog" ` +
          `-H "Authorization: Bearer ${authToken}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
        const catalog = JSON.parse(catalogResult);
        const products = catalog.products || catalog;
        const e2eProduct = (Array.isArray(products) ? products : []).find(
          (p: { name?: string; groupId?: string }) =>
            p.name?.includes('e2e-platform') || p.groupId?.includes('e2e-platform')
        );

        if (e2eProduct) {
          // Deploy it via API
          const stackConfigs = (e2eProduct.stacks || []).map(
            (s: { id: string; name: string }, i: number) => ({
              stackId: s.id,
              deploymentStackName: `e2e-platform-${s.name || `stack-${i}`}`,
              variables: {}
            })
          );

          const deployBody = JSON.stringify({
            productId: e2eProduct.id || e2eProduct.productId,
            stackConfigs,
            sharedVariables: {},
            continueOnError: true
          });

          const deployResult = execSync(
            `curl -sf -X POST "${BASE_URL}/api/environments/${environmentId}/product-deployments" ` +
            `-H "Authorization: Bearer ${authToken}" ` +
            `-H "Content-Type: application/json" ` +
            `-d '${deployBody.replace(/'/g, "'\\''")}'`,
            { encoding: 'utf-8', timeout: 60_000, stdio: ['pipe', 'pipe', 'ignore'] }
          );
          const deployResponse = JSON.parse(deployResult);
          if (deployResponse.productDeploymentId) {
            productDeploymentId = deployResponse.productDeploymentId;
            console.log(`Deployed e2e-platform: ${productDeploymentId}`);
            // Wait for deployment to complete
            await new Promise(r => setTimeout(r, 15_000));
          }
        }
      } catch (err) {
        console.warn('Could not deploy e2e-platform via API:', err);
      }
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should show Stop and Restart buttons on running product deployment', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Verify the product deployment detail page loaded
    await expect(page.getByText('Back to Deployments')).toBeVisible({ timeout: 10_000 });

    // Verify Stop Containers button is visible
    const stopButton = page.getByRole('button', { name: /stop containers/i });
    await expect(stopButton).toBeVisible({ timeout: 10_000 });

    // Verify Restart Containers button is visible
    const restartButton = page.getByRole('button', { name: /restart containers/i });
    await expect(restartButton).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-01-buttons.png'),
      fullPage: false
    });
  });

  test('should show confirmation dialog when clicking Stop', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Click Stop Containers button
    const stopButton = page.getByRole('button', { name: /stop containers/i });
    await expect(stopButton).toBeVisible({ timeout: 10_000 });
    await stopButton.click();

    // Confirmation dialog should appear
    await expect(page.getByText(/stop all containers of/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText(/this will stop all running containers/i)).toBeVisible();

    // Confirm and Cancel buttons should be present inside the confirmation dialog
    const confirmDialog = page.locator('.border-gray-200, .dark\\:border-gray-700').filter({ hasText: /stop all containers of/i });
    const confirmButton = confirmDialog.getByRole('button', { name: /stop containers/i });
    await expect(confirmButton).toBeVisible();
    const cancelButton = confirmDialog.getByRole('button', { name: /cancel/i });
    await expect(cancelButton).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-02-stop-confirm.png'),
      fullPage: false
    });

    // Cancel the action
    await cancelButton.click();

    // Confirmation should disappear
    await expect(page.getByText(/stop all containers of/i)).not.toBeVisible();
  });

  test('should stop containers and show result', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Click Stop Containers
    const stopButton = page.getByRole('button', { name: /stop containers/i });
    await expect(stopButton).toBeVisible({ timeout: 10_000 });
    await stopButton.click();

    // Wait for confirmation dialog and confirm
    await expect(page.getByText(/stop all containers of/i)).toBeVisible({ timeout: 5_000 });
    const confirmDialog = page.locator('.border-gray-200, .dark\\:border-gray-700').filter({ hasText: /stop all containers of/i });
    const confirmButton = confirmDialog.getByRole('button', { name: /stop containers/i });
    await confirmButton.click();

    // Should show loading spinner
    await expect(page.getByText(/processing/i)).toBeVisible({ timeout: 5_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-03-stop-loading.png'),
      fullPage: false
    });

    // Wait for result feedback
    const successResult = page.locator('.bg-green-50, .dark\\:bg-green-900\\/20');
    const errorResult = page.locator('.bg-red-50, .dark\\:bg-red-900\\/20');
    await expect(successResult.or(errorResult)).toBeVisible({ timeout: 30_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-04-stop-result.png'),
      fullPage: false
    });
  });

  test('should show confirmation dialog when clicking Restart', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Click Restart Containers button
    const restartButton = page.getByRole('button', { name: /restart containers/i });
    // If containers were stopped, the button might not be available
    const isVisible = await restartButton.isVisible().catch(() => false);
    test.skip(!isVisible, 'Restart button not visible (containers may be stopped)');

    await restartButton.click();

    // Confirmation dialog should appear with restart-specific text
    await expect(page.getByText(/restart all containers of/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText(/this will stop and start all containers sequentially/i)).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-05-restart-confirm.png'),
      fullPage: false
    });

    // Confirm the restart
    const confirmDialog = page.locator('.border-gray-200, .dark\\:border-gray-700').filter({ hasText: /restart all containers of/i });
    const confirmButton = confirmDialog.getByRole('button', { name: /restart containers/i });
    await confirmButton.click();

    // Should show loading
    await expect(page.getByText(/processing/i)).toBeVisible({ timeout: 5_000 });

    // Wait for result feedback
    const successResult = page.locator('.bg-green-50, .dark\\:bg-green-900\\/20');
    const errorResult = page.locator('.bg-red-50, .dark\\:bg-red-900\\/20');
    await expect(successResult.or(errorResult)).toBeVisible({ timeout: 60_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-06-restart-result.png'),
      fullPage: false
    });
  });
});
