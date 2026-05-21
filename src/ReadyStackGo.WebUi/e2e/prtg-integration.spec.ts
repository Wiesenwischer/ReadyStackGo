import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

const BASE_URL = 'http://localhost:8080';

/**
 * E2E spec that captures screenshots for the public PRTG integration docs.
 * Covers all four variants:
 *
 *   V1  Device Template Bundle    → Settings/SNMP "PRTG integration" card
 *   V3  Saved PrtgConnection      → Settings/PRTG Connections list + form
 *   V4  HTTP Data Advanced sensor → Settings/SNMP "PRTG HTTP sensor" card
 *   V2  Inline registration       → Deployment Detail "PRTG monitoring" card
 *                                   (requires an existing ProductDeployment —
 *                                   covered by a manual screenshot pass; the
 *                                   automated test here just makes sure the
 *                                   tabbed card layout renders cleanly on the
 *                                   settings flows we can fully script)
 *
 * Run via the temporary playwright config that points at the docker-compose
 * container on http://localhost:8080.
 */

const V3_TEST_CONNECTION = 'docs-screenshot-prtg';

test.describe('PRTG integration screenshots', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('captures the V1 Device Template Bundle card and the V4 HTTP sensor card on the SNMP settings page', async ({ page }) => {
    await page.goto('/settings/snmp');
    await page.waitForLoadState('networkidle');

    // V4 — "PRTG HTTP sensor" card is at the top of the PRTG block.
    const httpCard = page.getByRole('heading', { name: 'PRTG HTTP sensor' });
    await expect(httpCard).toBeVisible();
    await httpCard.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v4-http-sensor-card.png'),
      fullPage: false,
    });

    // V1 — "PRTG integration" card with Download bundle button.
    const bundleCard = page.getByRole('heading', { name: 'PRTG integration' });
    await expect(bundleCard).toBeVisible();
    await bundleCard.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v1-bundle-card.png'),
      fullPage: false,
    });
  });

  test('V3 — settings index has the PRTG Connections tile', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    const tile = page.getByText('PRTG Connections', { exact: true });
    await expect(tile).toBeVisible();
    await tile.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-settings-tile.png'),
      fullPage: false,
    });
  });

  test('V3 — empty PRTG Connections list', async ({ page }) => {
    await page.goto('/settings/prtg-connections');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'PRTG Connections' })).toBeVisible();
    // Take the screenshot regardless of empty/populated — the page renders the
    // same way both ways, but the empty-state message is a docs talking-point.
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-connections-list.png'),
      fullPage: false,
    });
  });

  test('V3 — Add connection form', async ({ page }) => {
    await page.goto('/settings/prtg-connections');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: '+ Add connection' }).click();
    await expect(page.getByRole('heading', { name: 'New PRTG connection' })).toBeVisible();

    // Fill the form so the screenshot shows a realistic example.
    await page.locator('label:has-text("Name") >> input').first().fill(V3_TEST_CONNECTION);
    await page.locator('label:has-text("URL") >> input').first().fill('https://prtg.example.local');
    await page.locator('label:has-text("API token") >> input').first().fill('PRTG_TOKEN_demo_value');
    await page.locator('label:has-text("Template Device ID") >> input').first().fill('4221');
    // VerifyTLS checkbox: leave as default (true) for the screenshot.

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-add-form.png'),
      fullPage: false,
    });

    // Submit + capture the resulting populated list — useful as a second
    // screenshot for the docs.
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.getByText(V3_TEST_CONNECTION).first()).toBeVisible({ timeout: 10000 });
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-v3-connections-list-populated.png'),
      fullPage: false,
    });
  });

  test('V3 — cleanup: delete the demo connection so the suite is rerunnable', async ({ page }) => {
    // Best-effort cleanup. The native confirm() dialog occasionally outruns
    // playwright's dialog handler, so we make this test idempotent rather
    // than failing the whole suite on a hygiene step.
    await page.goto('/settings/prtg-connections');
    await page.waitForLoadState('networkidle');

    const row = page.locator('tr', { hasText: V3_TEST_CONNECTION });
    if (await row.count() === 0) {
      return; // nothing to clean up
    }

    page.on('dialog', (d) => d.accept().catch(() => {}));
    await row.first().getByRole('button', { name: 'Delete' }).click();

    try {
      await expect(page.locator('tr', { hasText: V3_TEST_CONNECTION }))
        .toHaveCount(0, { timeout: 5000 });
    } catch {
      // ignore — best effort, the next test run reuses the same name
    }
  });
});

// ────────────────────────────────────────────────────────────────────
// Detail-page screenshots (V2 + V3 tabs on the PRTG monitoring card).
// Requires an active ProductDeployment; we reuse the e2e-platform fixture
// pattern from maintenance-mode.spec.ts to set one up if needed.
// ────────────────────────────────────────────────────────────────────

test.describe.serial('PRTG monitoring card on deployment detail', () => {
  test.setTimeout(180_000);

  let authToken = '';
  let environmentId = '';
  let productDeploymentId = '';

  test.beforeAll(async () => {
    authToken = curlJson<{ token: string }>(
      `-X POST ${BASE_URL}/api/auth/login -H "Content-Type: application/json" `
      + `-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}"`,
    ).token;

    const envs = curlJson<{ environments: { id: string }[] }>(
      `${BASE_URL}/api/environments -H "Authorization: Bearer ${authToken}"`,
    );
    environmentId = envs.environments[0].id;

    productDeploymentId = await ensureE2ePlatformDeployed(authToken, environmentId);
  });

  test.beforeEach(async ({ page }) => {
    test.skip(!productDeploymentId, 'No e2e-platform deployment — skipping detail-page screenshots');
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('captures the "Saved connection" tab on the deployment detail page', async ({ page }) => {
    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');

    const card = page.getByRole('heading', { name: 'PRTG monitoring' });
    await expect(card).toBeVisible();
    await card.scrollIntoViewIfNeeded();

    // Saved-connection tab is the default — just take the shot.
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-detail-saved-tab.png'),
      fullPage: false,
    });
  });

  test('captures the "Inline (ad-hoc)" tab (V2)', async ({ page }) => {
    await page.goto(`/product-deployments/${productDeploymentId}`);
    await page.waitForLoadState('networkidle');

    const card = page.getByRole('heading', { name: 'PRTG monitoring' });
    await expect(card).toBeVisible();
    await card.scrollIntoViewIfNeeded();

    await page.getByRole('button', { name: 'Inline (ad-hoc)' }).click();
    // Form should now be visible.
    await expect(page.getByPlaceholder('https://prtg.example.local')).toBeVisible();

    // Fill with demo values so the screenshot is informative.
    await page.getByPlaceholder('https://prtg.example.local').fill('https://prtg.example.local');
    await page.getByPlaceholder('PRTG API token or passhash').fill('PRTG_TOKEN_demo_value');
    await page.getByPlaceholder('e.g. 4221').fill('4221');

    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, 'prtg-detail-inline-tab.png'),
      fullPage: false,
    });
  });
});

// ── helpers ────────────────────────────────────────────────────────

function curlJson<T>(args: string): T {
  const out = execSync(`curl -sf ${args}`, { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] });
  return JSON.parse(out) as T;
}

function curlSilent(args: string): void {
  try {
    execSync(`curl -sf ${args}`, { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] });
  } catch {
    // ignore — the call is best-effort (resource might already exist, etc.)
  }
}

async function ensureE2ePlatformDeployed(authToken: string, environmentId: string): Promise<string> {
  // Already running?
  const existing = curlJson<{ productDeployments: { productDeploymentId: string; productName: string; status: string }[] }>(
    `${BASE_URL}/api/environments/${environmentId}/product-deployments `
    + `-H "Authorization: Bearer ${authToken}"`,
  );
  const already = existing.productDeployments.find(
    (p) => p.productName === 'E2E Platform' && (p.status === 'Running' || p.status === 'PartiallyRunning'),
  );
  if (already) return already.productDeploymentId;

  // Make sure the source exists and is synced.
  curlSilent(
    `-X POST ${BASE_URL}/api/stack-sources `
    + `-H "Authorization: Bearer ${authToken}" -H "Content-Type: application/json" `
    + `-d "{\\"id\\":\\"e2e-test-source\\",\\"name\\":\\"E2E Test Source\\",`
    + `\\"type\\":\\"LocalDirectory\\",\\"path\\":\\"/app/stacks/examples/e2e-platform\\"}"`,
  );
  curlSilent(`-X POST ${BASE_URL}/api/stack-sources/sync -H "Authorization: Bearer ${authToken}"`);

  // Deploy.
  const products = curlJson<{ id: string; name: string; stacks: { id: string }[] }[]>(
    `${BASE_URL}/api/products -H "Authorization: Bearer ${authToken}"`,
  );
  const product = products.find((p) => p.name === 'E2E Platform');
  if (!product) return '';

  const stackConfigs = product.stacks.map((s) => ({ stackId: s.id, variables: {} }));
  const deployBody = JSON.stringify({
    productId: product.id,
    deploymentName: 'e2e-platform',
    stackConfigs,
    continueOnError: true,
  });

  execSync(
    `curl -sf -X POST ${BASE_URL}/api/environments/${environmentId}/product-deployments `
    + `-H "Authorization: Bearer ${authToken}" -H "Content-Type: application/json" `
    + `-d @-`,
    { encoding: 'utf-8', input: deployBody, stdio: ['pipe', 'pipe', 'ignore'] },
  );

  // Poll up to 60s for Running / PartiallyRunning / Failed.
  for (let i = 0; i < 30; i++) {
    await new Promise((r) => setTimeout(r, 2000));
    try {
      const status = curlJson<{ productDeployments: { productDeploymentId: string; productName: string; status: string }[] }>(
        `${BASE_URL}/api/environments/${environmentId}/product-deployments `
        + `-H "Authorization: Bearer ${authToken}"`,
      );
      const pd = status.productDeployments.find((p) => p.productName === 'E2E Platform');
      if (pd && (pd.status === 'Running' || pd.status === 'PartiallyRunning' || pd.status === 'Failed')) {
        return pd.productDeploymentId;
      }
    } catch {
      // keep polling
    }
  }
  return '';
}
