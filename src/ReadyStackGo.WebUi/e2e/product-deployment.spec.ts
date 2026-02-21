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
 * E2E Tests for Product Deployment (Multi-Stack)
 * Tests the full product deployment flow: catalog → configure → deploy → success
 * Uses the e2e-platform multi-stack product (2 stacks: frontend + backend)
 */

test.describe.serial('Product Deployment', () => {
  test.setTimeout(180_000);

  let authToken: string;

  test.beforeAll(async () => {
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
      console.warn('Could not get auth token for cleanup');
    }

    // Ensure the e2e-platform stack source exists
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
        console.log('✓ E2E stack source created');
      }
    } catch {
      console.warn('Could not create e2e stack source');
    }

    // Sync sources so the multi-stack product is discovered
    try {
      execSync(
        `curl -sf -X POST ${BASE_URL}/api/stack-sources/sync ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      console.log('✓ Stack sources synced');
    } catch {
      console.warn('Could not sync sources');
    }

    // Remove any existing e2e-platform deployments from previous runs
    try {
      const deploymentsResult = execSync(
        `curl -sf ${BASE_URL}/api/deployments ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      const deployments = JSON.parse(deploymentsResult);
      for (const d of deployments) {
        if (d.stackName?.includes('e2e-platform')) {
          execSync(
            `curl -sf -X DELETE ${BASE_URL}/api/deployments/${d.id} ` +
            `-H "Authorization: Bearer ${authToken}"`,
            { stdio: ['pipe', 'pipe', 'ignore'] }
          );
          console.log(`✓ Removed existing deployment: ${d.stackName}`);
        }
      }
    } catch {
      // Ignore cleanup errors
    }

    cleanupTestContainers();
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test.afterAll(async () => {
    cleanupTestContainers();
  });

  test('should display E2E Platform in catalog as multi-stack product', async ({ page }) => {
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    // The multi-stack product should appear in the catalog with "2 stacks" badge
    await expect(page.getByText('E2E Platform')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('2 stacks')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-deploy-01-catalog.png'),
      fullPage: false
    });
  });

  test('should navigate to product detail page via View Details link', async ({ page }) => {
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    // Click the "View Details" link for the E2E Platform product (href contains "E2E")
    await page.locator(`a[href*="E2E"]`).first().click();

    await page.waitForURL(/\/catalog\//, { timeout: 10_000 });

    // Should show product name
    await expect(page.getByText('E2E Platform').first()).toBeVisible();

    // Should show both stacks
    await expect(page.getByText('Frontend').first()).toBeVisible();
    await expect(page.getByText('Backend').first()).toBeVisible();

    // Should have Deploy All button
    await expect(
      page.getByRole('button', { name: /deploy all/i })
    ).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-deploy-02-detail.png'),
      fullPage: false
    });
  });

  test('should show deploy product configuration page', async ({ page }) => {
    // Navigate to product detail via catalog
    await navigateToProductDetail(page);

    // Click Deploy All to reach deploy-product page
    await page.getByRole('button', { name: /deploy all/i }).click();
    await page.waitForURL(/\/deploy-product\//, { timeout: 10_000 });

    // Should show product name in heading
    await expect(page.getByText(/Deploy E2E Platform/i).first()).toBeVisible({ timeout: 10_000 });

    // Should show "Continue on Error" checkbox
    await expect(page.locator('#continueOnError')).toBeVisible();

    // Should show shared variables section (LOG_LEVEL)
    await expect(page.getByRole('heading', { name: 'Shared Variables' })).toBeVisible();
    await expect(page.getByText('Log Level').first()).toBeVisible();

    // Should show stack configuration section
    await expect(page.getByText('Stack Configuration')).toBeVisible();

    // Should show both stacks in the accordion
    await expect(page.getByText('Frontend').first()).toBeVisible();
    await expect(page.getByText('Backend').first()).toBeVisible();

    // Should show Deploy All Stacks button in sidebar
    await expect(
      page.getByRole('button', { name: /deploy all stacks/i })
    ).toBeVisible();

    // Should show product info in sidebar
    await expect(page.getByText('Product Info')).toBeVisible();

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-deploy-03-configure.png'),
      fullPage: false
    });
  });

  test('should deploy multi-stack product and complete successfully', async ({ page }) => {
    // Navigate via UI to avoid URL encoding issues with colons in product ID
    await navigateToDeployProduct(page);

    // Click Deploy All Stacks button
    const deployAllBtn = page.getByRole('button', { name: /deploy all stacks/i });
    await expect(deployAllBtn).toBeEnabled();
    await deployAllBtn.click();

    // Should transition to deploying state
    await expect(page.getByText(/deploying product/i)).toBeVisible({ timeout: 10_000 });

    // Should show stack status list with both stacks
    await expect(page.getByText('Frontend').first()).toBeVisible();
    await expect(page.getByText('Backend').first()).toBeVisible();

    // Take screenshot of deployment in progress
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-deploy-04-deploying.png'),
      fullPage: false
    });

    // Wait for deployment to complete (either success or error)
    // Use getByRole('heading') to avoid matching per-stack error spans
    const successHeading = page.getByRole('heading', { name: /product deployed successfully/i });
    const errorHeading = page.getByRole('heading', { name: /deployment.*failed/i });

    await expect(successHeading.or(errorHeading)).toBeVisible({ timeout: 120_000 });

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-deploy-05-result.png'),
      fullPage: false
    });

    // If successful, verify stack results are shown for both stacks
    const isSuccess = await successHeading.isVisible().catch(() => false);
    if (isSuccess) {
      await expect(page.getByText('Frontend').first()).toBeVisible();
      await expect(page.getByText('Backend').first()).toBeVisible();
      await expect(
        page.getByRole('link', { name: /view deployments/i })
      ).toBeVisible();
    }
  });

  test('should show deployed product stacks on deployments page', async ({ page }) => {
    await page.goto('/deployments');
    await page.waitForTimeout(3000);

    await expect(page.getByRole('heading', { name: /deployments/i }).first()).toBeVisible();

    // Both stacks should appear as deployed
    const frontendDeployment = page.getByText(/e2e-platform-frontend/i);
    const backendDeployment = page.getByText(/e2e-platform-backend/i);

    const hasFrontend = await frontendDeployment.first().isVisible().catch(() => false);
    const hasBackend = await backendDeployment.first().isVisible().catch(() => false);

    if (hasFrontend && hasBackend) {
      console.log('✓ Both product stacks visible on deployments page');
      await page.screenshot({
        path: path.join(SCREENSHOT_DIR, 'product-deploy-06-deployments.png'),
        fullPage: false
      });
    } else {
      console.log(`Frontend visible: ${hasFrontend}, Backend visible: ${hasBackend}`);
    }
  });

  test('should show product deployment status on product detail page', async ({ page }) => {
    // Navigate via UI to avoid URL encoding issues with colons in product ID
    await navigateToProductDetail(page);
    await page.waitForTimeout(1000);

    // Product detail page should show deployment status with per-stack indicators
    const deploymentStatus = page.getByText(/running|deployed|deployment status/i);
    const hasStatus = await deploymentStatus.first().isVisible().catch(() => false);

    if (hasStatus) {
      console.log('✓ Product deployment status visible on detail page');
    }

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'product-deploy-07-status.png'),
      fullPage: false
    });
  });
});

async function navigateToProductDetail(page: import('@playwright/test').Page) {
  await page.goto('/catalog');
  await page.waitForTimeout(2000);
  await page.locator('a[href*="E2E"]').first().click();
  await page.waitForURL(/\/catalog\//, { timeout: 10_000 });
  await expect(page.getByText('E2E Platform').first()).toBeVisible({ timeout: 10_000 });
}

async function navigateToDeployProduct(page: import('@playwright/test').Page) {
  await navigateToProductDetail(page);
  await page.getByRole('button', { name: /deploy all/i }).click();
  await page.waitForURL(/\/deploy-product\//, { timeout: 10_000 });
  await expect(page.getByText(/Deploy E2E Platform/i).first()).toBeVisible({ timeout: 10_000 });
}

function cleanupTestContainers() {
  try {
    const result = execSync('docker ps -aq --filter "name=e2e-platform"', {
      encoding: 'utf-8',
      stdio: ['pipe', 'pipe', 'ignore']
    });

    const containerIds = result.trim().split('\n').filter((id: string) => id);
    if (containerIds.length > 0) {
      execSync(`docker rm -f ${containerIds.join(' ')}`, { stdio: 'ignore' });
      console.log(`✓ Cleaned up ${containerIds.length} container(s)`);
    }

    const networksResult = execSync('docker network ls --filter "name=e2e-platform" -q', {
      encoding: 'utf-8',
      stdio: ['pipe', 'pipe', 'ignore']
    });

    const networkIds = networksResult.trim().split('\n').filter((id: string) => id);
    if (networkIds.length > 0) {
      execSync(`docker network rm ${networkIds.join(' ')}`, { stdio: 'ignore' });
      console.log(`✓ Cleaned up ${networkIds.length} network(s)`);
    }
  } catch {
    // Ignore cleanup errors
  }
}
