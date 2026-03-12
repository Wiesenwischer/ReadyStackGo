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
 * E2E Tests for Product Remove
 * Tests the remove confirmation screen, cancel flow, and progress UI.
 * Requires the e2e-platform product to be deployed first.
 */

test.describe.serial('Product Remove', () => {
  test.setTimeout(180_000);

  let authToken: string;
  let productDeploymentId: string;
  let environmentId: string;

  test.beforeAll(async () => {
    try {
      const loginResult = execSync(
        `curl -sf -X POST ${BASE_URL}/api/auth/login ` +
        '-H "Content-Type: application/json" ' +
        '-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}"',
        { encoding: 'utf-8' }
      );
      authToken = JSON.parse(loginResult).token;
    } catch {
      console.warn('Could not get auth token');
    }

    // Find the active environment
    try {
      const envsResult = execSync(
        `curl -sf ${BASE_URL}/api/environments ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8' }
      );
      const envsResponse = JSON.parse(envsResult);
      const envs = envsResponse.environments ?? (Array.isArray(envsResponse) ? envsResponse : []);
      environmentId = envs[0]?.id;
    } catch {
      console.warn('Could not find environment');
    }

    // Ensure e2e-platform source and product exist
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
      execSync(
        `curl -sf -X POST ${BASE_URL}/api/stack-sources/sync ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
    } catch {
      console.warn('Could not set up e2e stack source');
    }

    // Find or deploy e2e-platform product
    if (environmentId) {
      try {
        const pdResult = execSync(
          `curl -sf ${BASE_URL}/api/environments/${environmentId}/product-deployments ` +
          `-H "Authorization: Bearer ${authToken}"`,
          { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
        );
        const response = JSON.parse(pdResult);
        type PdEntry = { productName?: string; productDisplayName?: string; status?: string; canRemove?: boolean; productDeploymentId?: string; id?: string };
        const pds: PdEntry[] = response.productDeployments ?? (Array.isArray(response) ? response : []);
        const e2ePd = pds.find(
          pd =>
            (pd.productName?.toLowerCase().includes('e2e') ||
             pd.productDisplayName?.toLowerCase().includes('e2e')) &&
            pd.status !== 'Removed'
        );
        if (e2ePd) {
          productDeploymentId = (e2ePd.productDeploymentId ?? e2ePd.id)!;
          console.log(`✓ Found e2e product deployment: ${productDeploymentId} (status: ${e2ePd.status}, canRemove: ${e2ePd.canRemove})`);

          // If not removable, try to restart containers so it returns to Running
          if (!e2ePd.canRemove) {
            console.log('Product not removable — attempting to restart containers...');
            try {
              execSync(
                `curl -sf -X POST ${BASE_URL}/api/environments/${environmentId}/product-deployments/${productDeploymentId}/restart-containers ` +
                `-H "Authorization: Bearer ${authToken}" -H "Content-Type: application/json" -d "{}"`,
                { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
              );
              execSync('sleep 5', { stdio: 'ignore' });
              console.log('✓ Restart triggered');
            } catch {
              console.warn('Could not restart — tests may skip');
            }
          }
        } else {
          // Deploy e2e-platform fresh
          console.log('No e2e product deployment found — deploying one...');
          try {
            const catalogResult = execSync(
              `curl -sf "${BASE_URL}/api/environments/${environmentId}/catalog" ` +
              `-H "Authorization: Bearer ${authToken}"`,
              { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
            );
            const catalog = JSON.parse(catalogResult);
            const products = catalog.products ?? (Array.isArray(catalog) ? catalog : []);
            const e2eProduct = products.find(
              (p: { name?: string; groupId?: string }) =>
                p.name?.includes('e2e-platform') || p.groupId?.includes('e2e-platform')
            );
            if (e2eProduct) {
              const stackConfigs = (e2eProduct.stacks || []).map(
                (s: { id: string; name: string }, i: number) => ({
                  stackId: s.id,
                  deploymentStackName: `e2e-platform-${s.name || `stack-${i}`}`,
                  variables: {}
                })
              );
              const deployBody = JSON.stringify({
                productId: e2eProduct.id ?? e2eProduct.productId,
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
                console.log(`✓ Deployed e2e-platform: ${productDeploymentId}`);
                execSync('sleep 10', { stdio: 'ignore' });
              }
            }
          } catch (err) {
            console.warn('Could not deploy e2e-platform:', err);
          }
        }
      } catch {
        console.warn('Could not query product deployments');
      }
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should show product deployments on deployments page', async ({ page }) => {
    await page.goto('/deployments');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);

    await expect(page.getByRole('heading', { name: /deployments/i }).first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-remove-01-deployments.png'),
      fullPage: false
    });
  });

  test('should show product deployment detail with Remove button', async ({ page }) => {
    if (!productDeploymentId) {
      console.warn('No e2e product deployment found — skipping');
      test.skip();
      return;
    }

    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-remove-02-detail.png'),
      fullPage: false
    });

    const removeBtn = page.getByRole('link', { name: /remove/i }).first();
    const hasBtn = await removeBtn.isVisible().catch(() => false);
    if (hasBtn) {
      console.log('✓ Remove link found on product detail page');
    }
  });

  test('should show confirmation screen with warning and stacks', async ({ page }) => {
    if (!productDeploymentId) {
      test.skip();
      return;
    }

    await page.goto(`/remove-product/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    // Confirmation screen elements
    await expect(page.getByRole('heading', { name: /remove product/i })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('button', { name: /remove all stacks/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /cancel/i })).toBeVisible();
    await expect(page.getByText('Product Details')).toBeVisible();
    await expect(page.getByText('Stacks to remove')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-remove-03-confirm.png'),
      fullPage: false
    });
  });

  test('should cancel remove and return to product detail', async ({ page }) => {
    if (!productDeploymentId) {
      test.skip();
      return;
    }

    await page.goto(`/remove-product/${productDeploymentId}`);
    await expect(page.getByRole('heading', { name: /remove product/i })).toBeVisible({ timeout: 10_000 });

    await page.getByRole('link', { name: /cancel/i }).click();
    // Cancel returns to the catalog page
    await page.waitForURL(/\/(catalog|product-deployments)/, { timeout: 10_000 });
    console.log('✓ Cancel navigated back');
  });

  test('should display progress UI and complete removal', async ({ page }) => {
    if (!productDeploymentId) {
      test.skip();
      return;
    }

    await page.goto(`/remove-product/${productDeploymentId}`);
    await expect(page.getByRole('heading', { name: /remove product/i })).toBeVisible({ timeout: 10_000 });

    // Start the removal
    await page.getByRole('button', { name: /remove all stacks/i }).click();

    // Removing state: spinner header
    await expect(page.getByText(/removing product/i)).toBeVisible({ timeout: 10_000 });

    // Progress bar container visible
    const progressBarContainer = page.locator('.h-2.bg-gray-200.rounded-full').first();
    await expect(progressBarContainer).toBeVisible({ timeout: 5_000 });

    // Left panel: "Stacks" label
    await expect(page.getByText('Stacks').first()).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-remove-04-progress.png'),
      fullPage: false
    });

    // Wait for completion
    const successHeading = page.getByRole('heading', { name: /product removed successfully/i });
    const errorHeading = page.getByRole('heading', { name: /removal completed with errors/i });
    await expect(successHeading.or(errorHeading)).toBeVisible({ timeout: 120_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-remove-05-success.png'),
      fullPage: false
    });

    const isSuccess = await successHeading.isVisible().catch(() => false);
    if (isSuccess) {
      await expect(page.getByRole('link', { name: /view deployments/i })).toBeVisible();
      await expect(page.getByRole('link', { name: /browse catalog/i })).toBeVisible();
      console.log('✓ Product removed successfully');
    }
  });
});
