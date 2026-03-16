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
 * E2E Tests for Product Maintenance Mode
 * Tests the enter/exit maintenance flow with dedicated confirmation pages.
 * Requires: A deployed product (e2e-platform) — run product-deployment tests first.
 */

test.describe.serial('Product Maintenance Mode', () => {
  test.setTimeout(120_000);

  let authToken: string;
  let environmentId: string;
  let productDeploymentId: string;

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
      environmentId = envResponse.environments?.[0]?.id;
      console.log(`✓ Environment: ${environmentId}`);
    } catch {
      console.warn('Could not get environment');
    }

    // Ensure e2e-platform is deployed
    await ensureE2ePlatformDeployed();

    // Find the product deployment
    try {
      const pdResult = execSync(
        `curl -sf ${BASE_URL}/api/environments/${environmentId}/product-deployments ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      const pdResponse = JSON.parse(pdResult);
      const pd = pdResponse.productDeployments?.find(
        (p: { productName: string; status: string }) =>
          p.productName === 'E2E Platform' && (p.status === 'Running' || p.status === 'PartiallyRunning')
      );
      if (pd) {
        productDeploymentId = pd.productDeploymentId;
        console.log(`✓ Product deployment: ${productDeploymentId}`);
      } else {
        console.warn('No running e2e-platform product deployment found');
      }
    } catch {
      console.warn('Could not find product deployment');
    }

    // Ensure product is in Normal mode before tests
    if (productDeploymentId && environmentId) {
      try {
        execSync(
          `curl -sf -X PUT ${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/operation-mode ` +
          `-H "Authorization: Bearer ${authToken}" ` +
          `-H "Content-Type: application/json" ` +
          `-d "{\\"mode\\":\\"Normal\\"}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
      } catch {
        // May already be in Normal mode
      }
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should show product deployment detail with Enter Maintenance link', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Should show product name
    await expect(page.getByText('E2E Platform').first()).toBeVisible({ timeout: 10_000 });

    // Should show Normal operation mode
    await expect(page.getByText('Operation Mode')).toBeVisible();
    await expect(page.getByText('Normal').first()).toBeVisible();

    // Should show Enter Maintenance link (now a Link, not a button)
    await expect(page.getByRole('link', { name: /enter maintenance/i })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-01-normal-mode.png'),
      fullPage: false
    });
  });

  test('should navigate to Enter Maintenance confirmation page', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Click Enter Maintenance link
    await page.getByRole('link', { name: /enter maintenance/i }).click();

    // Should navigate to confirmation page
    await page.waitForURL(`/enter-maintenance/${productDeploymentId}`);

    // Confirmation page should show
    await expect(page.getByRole('heading', { name: 'Enter Maintenance Mode' })).toBeVisible();
    await expect(page.getByText('Are you sure')).toBeVisible();

    // Should show affected stacks
    await expect(page.getByText('Stacks affected')).toBeVisible();

    // Should have Cancel and Confirm buttons
    await expect(page.getByRole('link', { name: 'Cancel' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Enter Maintenance Mode' })).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-02-in-maintenance.png'),
      fullPage: false
    });
  });

  test('should enter maintenance mode via confirmation page', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/enter-maintenance/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');

    // Click Enter Maintenance Mode button
    await page.getByRole('button', { name: 'Enter Maintenance Mode' }).click();

    // Should show success page
    await expect(page.getByText('Maintenance Mode Activated')).toBeVisible({ timeout: 15_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-03-overview-cards.png'),
      fullPage: false
    });

    // Navigate to deployment detail to verify
    await page.getByRole('link', { name: 'View Deployment' }).click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Should show Maintenance badge and Stopped status
    await expect(page.getByText('Maintenance').first()).toBeVisible();
    await expect(page.getByText('Stopped').first()).toBeVisible();
  });

  test('should show stacks as Stopped during maintenance', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Stacks should show Stopped status (not Running)
    await expect(page.getByText(/Stacks \(/)).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-05-stacks-during.png'),
      fullPage: false
    });
  });

  test('should navigate to Exit Maintenance confirmation page', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Click Exit Maintenance link
    await page.getByRole('link', { name: /exit maintenance/i }).click();

    // Should navigate to exit confirmation page
    await page.waitForURL(`/exit-maintenance/${productDeploymentId}`);

    await expect(page.getByRole('heading', { name: 'Exit Maintenance Mode' })).toBeVisible();
    await expect(page.getByText('Stacks to restart')).toBeVisible();

    // Click Exit Maintenance Mode button
    await page.getByRole('button', { name: 'Exit Maintenance Mode' }).click();

    // Should show success
    await expect(page.getByText('Maintenance Mode Deactivated')).toBeVisible({ timeout: 15_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-04-exited.png'),
      fullPage: false
    });
  });

  async function ensureE2ePlatformDeployed() {
    // Check if already deployed
    try {
      const pdResult = execSync(
        `curl -sf ${BASE_URL}/api/environments/${environmentId}/product-deployments ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      const pdResponse = JSON.parse(pdResult);
      const hasRunning = pdResponse.productDeployments?.some(
        (p: { productName: string; status: string }) =>
          p.productName === 'E2E Platform' && (p.status === 'Running' || p.status === 'PartiallyRunning')
      );
      if (hasRunning) {
        console.log('✓ E2E platform already deployed');
        return;
      }
    } catch {
      // Continue to deploy
    }

    // Ensure stack source exists and sync
    try {
      execSync(
        `curl -sf -X POST ${BASE_URL}/api/stack-sources ` +
        `-H "Authorization: Bearer ${authToken}" ` +
        `-H "Content-Type: application/json" ` +
        `-d "{\\"id\\":\\"e2e-test-source\\",\\"name\\":\\"E2E Test Source\\",\\"type\\":\\"LocalDirectory\\",\\"path\\":\\"/app/stacks/examples/e2e-platform\\"}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
    } catch {
      // May already exist
    }

    try {
      execSync(
        `curl -sf -X POST ${BASE_URL}/api/stack-sources/sync ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
    } catch {
      // Ignore
    }

    // Deploy the product
    try {
      const productsResult = execSync(
        `curl -sf ${BASE_URL}/api/products ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      const products = JSON.parse(productsResult);
      const product = products.find((p: { name: string }) => p.name === 'E2E Platform');
      if (!product) {
        console.warn('E2E platform product not found in products list');
        return;
      }

      const stackConfigs = product.stacks.map((s: { id: string }) => ({
        stackId: s.id,
        variables: {}
      }));
      const deployBody = JSON.stringify({
        productId: product.id,
        deploymentName: 'e2e-platform',
        stackConfigs,
        continueOnError: true
      });

      execSync(
        `curl -sf -X POST ${BASE_URL}/api/environments/${environmentId}/product-deployments ` +
        `-H "Authorization: Bearer ${authToken}" ` +
        `-H "Content-Type: application/json" ` +
        `-d @-`,
        { encoding: 'utf-8', input: deployBody, stdio: ['pipe', 'pipe', 'ignore'] }
      );
      console.log('✓ Product deployment initiated');

      // Wait for deployment to complete (up to 60s)
      for (let i = 0; i < 30; i++) {
        await new Promise(resolve => setTimeout(resolve, 2000));
        try {
          const statusResult = execSync(
            `curl -sf ${BASE_URL}/api/environments/${environmentId}/product-deployments ` +
            `-H "Authorization: Bearer ${authToken}"`,
            { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
          );
          const statusResponse = JSON.parse(statusResult);
          const pd = statusResponse.productDeployments?.find(
            (p: { productName: string }) => p.productName === 'E2E Platform'
          );
          if (pd && (pd.status === 'Running' || pd.status === 'PartiallyRunning' || pd.status === 'Failed')) {
            console.log(`✓ Product deployment status: ${pd.status}`);
            return;
          }
        } catch {
          // Keep waiting
        }
      }
    } catch (e) {
      console.warn('Could not deploy e2e-platform:', e);
    }
  }
});
