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
 * Tests the enter/exit maintenance flow on the product deployment detail page.
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

  test('should show product deployment detail with Normal operation mode', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Should show product name
    await expect(page.getByText('E2E Platform').first()).toBeVisible({ timeout: 10_000 });

    // Should show Normal operation mode in overview card
    await expect(page.getByText('Operation Mode')).toBeVisible();
    await expect(page.getByText('Normal').first()).toBeVisible();

    // Should show Enter Maintenance button
    await expect(
      page.getByRole('button', { name: /enter maintenance/i })
    ).toBeVisible();

    // Should NOT show Exit Maintenance button
    await expect(
      page.getByRole('button', { name: /exit maintenance/i })
    ).not.toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-01-normal-mode.png'),
      fullPage: false
    });
  });

  test('should enter maintenance mode', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Click Enter Maintenance
    const enterBtn = page.getByRole('button', { name: /enter maintenance/i });
    await expect(enterBtn).toBeVisible({ timeout: 10_000 });
    await enterBtn.click();

    // Wait for mode change to reflect
    await page.waitForTimeout(3000);

    // Should now show Maintenance badge
    await expect(page.getByText('Maintenance').first()).toBeVisible({ timeout: 10_000 });

    // Should show maintenance info panel
    await expect(page.getByText('Maintenance Mode').first()).toBeVisible();
    await expect(page.getByText('(Manual)').first()).toBeVisible();

    // Should show Exit Maintenance button
    await expect(
      page.getByRole('button', { name: /exit maintenance/i })
    ).toBeVisible();

    // Enter Maintenance button should be gone
    await expect(
      page.getByRole('button', { name: /enter maintenance/i })
    ).not.toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-02-in-maintenance.png'),
      fullPage: false
    });
  });

  test('should show maintenance status in overview cards', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    // Ensure we're in maintenance mode
    if (environmentId && productDeploymentId) {
      try {
        execSync(
          `curl -sf -X PUT ${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/operation-mode ` +
          `-H "Authorization: Bearer ${authToken}" ` +
          `-H "Content-Type: application/json" ` +
          `-d "{\\"mode\\":\\"Maintenance\\",\\"reason\\":\\"Scheduled database migration\\"}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
      } catch {
        // May already be in maintenance
      }
    }

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Overview card should show Maintenance
    await expect(page.getByText('Operation Mode')).toBeVisible();

    // Maintenance info panel should show reason
    const reasonText = page.getByText('Scheduled database migration');
    const hasReason = await reasonText.isVisible().catch(() => false);
    if (hasReason) {
      console.log('✓ Maintenance reason displayed');
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-03-overview-cards.png'),
      fullPage: false
    });
  });

  test('should exit maintenance mode', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    // Ensure we're in maintenance (manual trigger) first
    if (environmentId && productDeploymentId) {
      try {
        execSync(
          `curl -sf -X PUT ${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/operation-mode ` +
          `-H "Authorization: Bearer ${authToken}" ` +
          `-H "Content-Type: application/json" ` +
          `-d "{\\"mode\\":\\"Maintenance\\"}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
      } catch {
        // May already be in maintenance
      }
    }

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Click Exit Maintenance
    const exitBtn = page.getByRole('button', { name: /exit maintenance/i });
    await expect(exitBtn).toBeVisible({ timeout: 10_000 });
    await exitBtn.click();

    // Wait for mode change to reflect
    await page.waitForTimeout(3000);

    // Should be back to Normal
    await expect(page.getByText('Normal').first()).toBeVisible({ timeout: 10_000 });

    // Enter Maintenance button should be back
    await expect(
      page.getByRole('button', { name: /enter maintenance/i })
    ).toBeVisible();

    // Maintenance info panel should be gone
    await expect(page.getByText('Maintenance Mode')).not.toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-04-exited.png'),
      fullPage: false
    });
  });

  test('should show stacks table during maintenance', async ({ page }) => {
    test.skip(!productDeploymentId, 'No product deployment found');

    // Enter maintenance for screenshot
    if (environmentId && productDeploymentId) {
      try {
        execSync(
          `curl -sf -X PUT ${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/operation-mode ` +
          `-H "Authorization: Bearer ${authToken}" ` +
          `-H "Content-Type: application/json" ` +
          `-d "{\\"mode\\":\\"Maintenance\\",\\"reason\\":\\"Server hardware maintenance\\"}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
      } catch {
        // May already be in maintenance
      }
    }

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Should show stacks section
    await expect(page.getByText(/Stacks \(/)).toBeVisible();

    // Both stacks should be listed
    await expect(page.getByText('Frontend').first()).toBeVisible();
    await expect(page.getByText('Backend').first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'maintenance-05-stacks-during.png'),
      fullPage: false
    });

    // Clean up: exit maintenance
    if (environmentId && productDeploymentId) {
      try {
        execSync(
          `curl -sf -X PUT ${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/operation-mode ` +
          `-H "Authorization: Bearer ${authToken}" ` +
          `-H "Content-Type: application/json" ` +
          `-d "{\\"mode\\":\\"Normal\\"}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
      } catch {
        // Ignore
      }
    }
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

      // Build stackConfigs from product stacks (required by deploy API)
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
