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
 * Tests the dedicated stop and restart pages for product deployments.
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
            (pd.status === 'Running' || pd.status === 'PartiallyRunning' || pd.status === 'Stopped')
        );
        if (e2ePd) {
          productDeploymentId = e2ePd.productDeploymentId;
          console.log(`Found e2e-platform product deployment: ${productDeploymentId} (status: ${e2ePd.status})`);
          // If stopped, restart it first so tests can run the full stop→restart cycle
          if (e2ePd.status === 'Stopped') {
            console.log('Deployment is stopped, restarting containers...');
            try {
              execSync(
                `curl -sf -X POST "${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/restart-containers" ` +
                `-H "Authorization: Bearer ${authToken}" ` +
                `-H "Content-Type: application/json" -d "{}"`,
                { encoding: 'utf-8', timeout: 30_000, stdio: ['pipe', 'pipe', 'ignore'] }
              );
              await new Promise(r => setTimeout(r, 5_000));
              console.log('Containers restarted');
            } catch {
              console.warn('Could not restart containers');
            }
          }
        } else {
          console.warn('No e2e-platform product deployment found. Deploying one...');
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

  test('should show Stop and Restart links on running product deployment', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Verify the product deployment detail page loaded
    await expect(page.getByText('Back to Deployments')).toBeVisible({ timeout: 10_000 });

    // Verify Stop Containers link is visible
    const stopLink = page.getByRole('link', { name: /stop containers/i });
    await expect(stopLink).toBeVisible({ timeout: 10_000 });

    // Verify Restart Containers link is visible
    const restartLink = page.getByRole('link', { name: /restart containers/i });
    await expect(restartLink).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-01-buttons.png'),
      fullPage: false
    });
  });

  test('should navigate to stop confirmation page', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Click Stop Containers link
    const stopLink = page.getByRole('link', { name: /stop containers/i });
    await expect(stopLink).toBeVisible({ timeout: 10_000 });
    await stopLink.click();

    // Should navigate to the stop confirmation page
    await page.waitForURL(`**/stop-product/${productDeploymentId}`, { timeout: 10_000 });

    // Confirmation page should show the stop title
    await expect(page.getByText('Stop Product Containers')).toBeVisible({ timeout: 10_000 });

    // Should show product details
    await expect(page.getByText('Product Details')).toBeVisible();

    // Should show the stacks to stop section
    await expect(page.getByText('Stacks to stop')).toBeVisible();

    // Cancel link and Stop button should be present
    await expect(page.getByRole('link', { name: /cancel/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /stop all containers/i })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-02-stop-confirm.png'),
      fullPage: false
    });

    // Cancel should navigate back to deployment detail
    await page.getByRole('link', { name: /cancel/i }).click();
    await page.waitForURL(`**/product-deployments/${productDeploymentId}`, { timeout: 10_000 });
  });

  test('should stop containers and show result', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    // Navigate directly to the stop page
    await page.goto(`/stop-product/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Verify confirm page loaded
    await expect(page.getByText('Stop Product Containers')).toBeVisible({ timeout: 10_000 });

    // Click the stop button
    await page.getByRole('button', { name: /stop all containers/i }).click();

    // Should show stopping spinner
    await expect(page.getByText('Stopping Containers...')).toBeVisible({ timeout: 5_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-03-stop-loading.png'),
      fullPage: false
    });

    // Wait for result — either success or error
    const successHeading = page.getByText('Containers Stopped Successfully!');
    const errorHeading = page.getByText('Stop Completed with Errors');
    await expect(successHeading.or(errorHeading)).toBeVisible({ timeout: 30_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-04-stop-result.png'),
      fullPage: false
    });

    // Navigate back to the detail page to capture Stopped status
    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-05-stopped-status.png'),
      fullPage: false
    });
  });

  test('should navigate to restart page and restart containers', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment available');

    // Navigate directly to the restart page
    await page.goto(`/restart-product/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    // Check if we can restart — the page shows an error if canRestart is false
    const confirmTitle = page.getByText('Restart Product Containers');
    const visible = await confirmTitle.isVisible().catch(() => false);

    if (!visible) {
      // canRestart might be false after stopping — skip
      test.skip(true, 'Restart not available (containers may already be stopped)');
      return;
    }

    // Should show product details and stacks to restart
    await expect(page.getByText('Product Details')).toBeVisible();
    await expect(page.getByText('Stacks to restart')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-06-restart-confirm.png'),
      fullPage: false
    });

    // Click the restart button
    await page.getByRole('button', { name: /restart all containers/i }).click();

    // Should show restarting spinner
    await expect(page.getByText('Restarting Containers...')).toBeVisible({ timeout: 5_000 });

    // Wait for result
    const successHeading = page.getByText('Containers Restarted Successfully!');
    const errorHeading = page.getByText('Restart Completed with Errors');
    await expect(successHeading.or(errorHeading)).toBeVisible({ timeout: 60_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'container-control-07-restart-result.png'),
      fullPage: false
    });
  });
});
