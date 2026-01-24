import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';
import { execSync } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

/**
 * E2E Tests for Environment Variable Persistence
 * Tests that environment variables are saved and auto-loaded for deployments (v0.17 feature)
 */

test.describe.serial('Environment Variable Persistence', () => {
  // Clean up any leftover containers and networks from previous test runs
  test.beforeAll(async () => {
    try {
      // Clean up containers
      const result = execSync('docker ps -aq --filter "name=e2e-"', {
        encoding: 'utf-8',
        stdio: ['pipe', 'pipe', 'ignore']
      });

      const containerIds = result.trim().split('\n').filter(id => id);

      if (containerIds.length > 0) {
        execSync(`docker rm -f ${containerIds.join(' ')}`, {
          stdio: 'ignore'
        });
        console.log(`✓ Cleaned up ${containerIds.length} leftover container(s) from previous runs`);
      }

      // Clean up networks
      const networksResult = execSync('docker network ls --filter "name=e2e-" -q', {
        encoding: 'utf-8',
        stdio: ['pipe', 'pipe', 'ignore']
      });

      const networkIds = networksResult.trim().split('\n').filter(id => id);

      if (networkIds.length > 0) {
        execSync(`docker network rm ${networkIds.join(' ')}`, {
          stdio: 'ignore'
        });
        console.log(`✓ Cleaned up ${networkIds.length} leftover network(s) from previous runs`);
      }
    } catch {
      // Ignore errors
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test.afterEach(async () => {
    // Cleanup containers and networks created during tests to avoid conflicts
    try {
      // Clean up containers
      const result = execSync('docker ps -aq --filter "name=e2e-"', {
        encoding: 'utf-8',
        stdio: ['pipe', 'pipe', 'ignore']
      });

      const containerIds = result.trim().split('\n').filter(id => id);

      if (containerIds.length > 0) {
        execSync(`docker rm -f ${containerIds.join(' ')}`, {
          stdio: 'ignore'
        });
        console.log(`✓ Cleaned up ${containerIds.length} test container(s)`);
      }

      // Clean up networks
      const networksResult = execSync('docker network ls --filter "name=e2e-" -q', {
        encoding: 'utf-8',
        stdio: ['pipe', 'pipe', 'ignore']
      });

      const networkIds = networksResult.trim().split('\n').filter(id => id);

      if (networkIds.length > 0) {
        execSync(`docker network rm ${networkIds.join(' ')}`, {
          stdio: 'ignore'
        });
        console.log(`✓ Cleaned up ${networkIds.length} test network(s)`);
      }
    } catch {
      // Ignore errors - resources might not exist
    }
  });

  test('should auto-save and auto-load variables for catalog stack', async ({ page }) => {
    // Navigate to catalog
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    // Find and click "View Details" for E2E test stack
    await expect(page.getByText('E2E Test Stack').first()).toBeVisible({ timeout: 10000 });

    // Find the product card that contains "E2E Test Stack" and click its "View Details" link
    const productCard = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard.getByRole('link', { name: /view details/i }).click();

    // Wait for product detail page
    await page.waitForTimeout(1000);

    // Click Deploy button on product detail page
    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);

    // Wait for the deploy form to load completely
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Stack Configuration')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Environment Variables' })).toBeVisible();

    // Fill in stack name
    const stackName = `e2e-test-${Date.now()}`;
    const stackNameInput = page.locator('text=Stack Name').locator('..').locator('input');
    await expect(stackNameInput).toBeVisible();
    await stackNameInput.fill(stackName);

    // Fill in variables with test values
    // Note: Variable inputs don't have proper id/name/htmlFor, so we use placeholder or label text + input
    await page.locator('text=Application Name').locator('..').locator('input').fill('myapp');
    await page.locator('text=Application Port').locator('..').locator('input').fill('9001');
    await page.locator('text=Database URL').locator('..').locator('input').fill('postgres://testserver:5432/mydb');
    await page.locator('text=API Key').locator('..').locator('input').fill('test-api-key-123');

    console.log('✓ Variables filled with custom values');

    // Deploy the stack - this triggers variable persistence
    await page.getByRole('button', { name: /deploy to/i }).click();

    // Wait for deployment to complete successfully
    // Variables are only saved AFTER successful deployment
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ First deployment completed with custom variables');

    // Now deploy the same stack again to test auto-load
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    // Click "View Details" for E2E test stack again
    const productCard2 = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard2.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    // Click Deploy button on product detail page
    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);

    // Wait for the deploy form to load
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Stack Configuration')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Environment Variables' })).toBeVisible();

    // Fill in a different stack name
    const stackName2 = `e2e-test-2-${Date.now()}`;
    const stackNameInput2 = page.locator('text=Stack Name').locator('..').locator('input');
    await expect(stackNameInput2).toBeVisible();
    await stackNameInput2.fill(stackName2);

    // Check if variables were auto-loaded from previous form interaction
    const appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
    const appPortValue = await page.locator('text=Application Port').locator('..').locator('input').inputValue();
    const dbUrlValue = await page.locator('text=Database URL').locator('..').locator('input').inputValue();

    expect(appNameValue).toBe('myapp');
    expect(appPortValue).toBe('9001');
    expect(dbUrlValue).toBe('postgres://testserver:5432/mydb');

    console.log('✓ Variables were auto-loaded from previous form interaction');
  });

  test('should allow resetting variables to defaults', async ({ page }) => {
    // Navigate to catalog and deploy test stack
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    // Find and click "View Details" for E2E test stack
    await expect(page.getByText('E2E Test Stack').first()).toBeVisible({ timeout: 10000 });

    // Find the product card that contains "E2E Test Stack" and click its "View Details" link
    const productCard = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    // Click Deploy button on product detail page
    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);

    // Wait for the deploy form to load
    await expect(page.getByText('Stack Configuration')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Environment Variables' })).toBeVisible();

    // Change variables from defaults
    await page.locator('text=Application Name').locator('..').locator('input').fill('custom-app');
    await page.locator('text=Application Port').locator('..').locator('input').fill('7777');

    // Click Reset to Defaults button
    await page.getByRole('button', { name: /reset to defaults/i }).click();

    // Verify variables were reset to defaults
    const appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
    const appPortValue = await page.locator('text=Application Port').locator('..').locator('input').inputValue();

    expect(appNameValue).toBe('testapp');  // default value from manifest
    expect(appPortValue).toBe('8080');     // default value from manifest

    console.log('✓ Variables reset to defaults successfully');
  });

  test('should persist sensitive variable values', async ({ page }) => {
    // Navigate to catalog
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    // Find and click "View Details" for E2E test stack
    await expect(page.getByText('E2E Test Stack').first()).toBeVisible({ timeout: 10000 });

    const productCard = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    // Click Deploy button
    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);

    // Wait for form to load
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Stack Configuration')).toBeVisible();

    // Fill in stack name and variables including sensitive API_KEY
    const stackName = `e2e-sensitive-${Date.now()}`;
    await page.locator('text=Stack Name').locator('..').locator('input').fill(stackName);
    await page.locator('text=Application Name').locator('..').locator('input').fill('secure-app');
    await page.locator('text=Application Port').locator('..').locator('input').fill('8888');
    await page.locator('text=API Key').locator('..').locator('input').fill('super-secret-key-12345');

    // Verify API Key input is password type (masked)
    const apiKeyInput = page.locator('text=API Key').locator('..').locator('input');
    const inputType = await apiKeyInput.getAttribute('type');
    expect(inputType).toBe('password');

    console.log('✓ Sensitive field is password type (masked)');

    // Deploy the stack
    await page.getByRole('button', { name: /deploy to/i }).click();
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ Deployment with sensitive variable completed');

    // Navigate back and deploy again to test auto-load
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    const productCard2 = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard2.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Verify sensitive variable was auto-loaded
    const apiKeyValue = await page.locator('text=API Key').locator('..').locator('input').inputValue();
    expect(apiKeyValue).toBe('super-secret-key-12345');

    // Verify it's still password type
    const apiKeyInput2 = page.locator('text=API Key').locator('..').locator('input');
    const inputType2 = await apiKeyInput2.getAttribute('type');
    expect(inputType2).toBe('password');

    console.log('✓ Sensitive variable auto-loaded and remains masked');
  });

  test('should update variable values across deployments', async ({ page }) => {
    // Navigate to catalog
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    await expect(page.getByText('E2E Test Stack').first()).toBeVisible({ timeout: 10000 });

    // First deployment with initial values
    const productCard = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Reset to defaults first to ensure clean state (previous tests may have saved different values)
    await page.getByRole('button', { name: /reset to defaults/i }).click();
    await page.waitForTimeout(500);

    const stackName1 = `e2e-update-v1-${Date.now()}`;
    await page.locator('text=Stack Name').locator('..').locator('input').fill(stackName1);
    await page.locator('text=Application Name').locator('..').locator('input').fill('myapp-v1');
    await page.locator('text=Application Port').locator('..').locator('input').fill('9002');
    await page.locator('text=Database URL').locator('..').locator('input').fill('postgres://server1/db');

    await page.getByRole('button', { name: /deploy to/i }).click();
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ First deployment completed with v1 values');

    // Second deployment with updated values
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    const productCard2 = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard2.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Verify v1 values are auto-loaded
    let appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
    expect(appNameValue).toBe('myapp-v1');

    // Update to v2 values - clear fields first to ensure old values are removed
    await page.waitForTimeout(500);
    const stackName2 = `e2e-update-v2-${Date.now()}`;
    const stackNameInput = page.locator('text=Stack Name').locator('..').locator('input');
    await stackNameInput.clear();
    await stackNameInput.fill(stackName2);

    const appNameInput = page.locator('text=Application Name').locator('..').locator('input');
    await appNameInput.clear();
    await appNameInput.fill('myapp-v2');

    const appPortInput = page.locator('text=Application Port').locator('..').locator('input');
    await appPortInput.clear();
    await appPortInput.fill('9003');

    const dbUrlInput = page.locator('text=Database URL').locator('..').locator('input');
    await dbUrlInput.clear();
    await dbUrlInput.fill('postgres://server2/db');

    await page.getByRole('button', { name: /deploy to/i }).click();
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ Second deployment completed with v2 values');

    // Third deployment to verify v2 values persisted (not v1)
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    const productCard3 = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard3.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Verify v2 values are loaded (not v1)
    appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
    const appPortValue = await page.locator('text=Application Port').locator('..').locator('input').inputValue();
    const dbUrlValue = await page.locator('text=Database URL').locator('..').locator('input').inputValue();

    expect(appNameValue).toBe('myapp-v2');
    expect(appPortValue).toBe('9003');
    expect(dbUrlValue).toBe('postgres://server2/db');

    console.log('✓ Latest deployment values correctly overwrote previous values');
  });

  // KNOWN BUG: Auto-load does not isolate variables by product ID
  // Variables are currently loaded by (environment + key) only, not by (environment + product + key)
  // This causes Stack B's values to overwrite Stack A's values when both have the same variable keys
  test.fail('should isolate variables between different stacks', async ({ page }) => {
    test.setTimeout(180000); // This test deploys 2 stacks, needs more time

    // Deploy Stack A (E2E Test Stack) with specific values
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    // Match "E2E Test Stack" but not "E2E Test Stack B" (negative lookahead)
    await expect(page.getByText(/^E2E Test Stack(?! B)/).first()).toBeVisible({ timeout: 10000 });

    const productCardA = page.locator('div').filter({ hasText: /^E2E Test Stack(?! B)/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCardA.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    const stackNameA = `e2e-stack-a-${Date.now()}`;
    await page.locator('text=Stack Name').locator('..').locator('input').fill(stackNameA);
    await page.locator('text=Application Name').locator('..').locator('input').fill('stack-a-app');
    await page.locator('text=Application Port').locator('..').locator('input').fill('9004');

    await page.getByRole('button', { name: /deploy to/i }).click();
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ Stack A deployed with values');

    // Deploy Stack B (E2E Test Stack B) with different values
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    await expect(page.getByText('E2E Test Stack B').first()).toBeVisible({ timeout: 10000 });

    const productCardB = page.locator('div').filter({ hasText: /^E2E Test Stack B/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCardB.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    const stackNameB = `e2e-stack-b-${Date.now()}`;
    await page.locator('text=Stack Name').locator('..').locator('input').fill(stackNameB);
    await page.locator('text=Application Name').locator('..').locator('input').fill('stack-b-app');
    await page.locator('text=Application Port').locator('..').locator('input').fill('9005');

    await page.getByRole('button', { name: /deploy to/i }).click();
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ Stack B deployed with different values');

    // Redeploy Stack A and verify it has Stack A values (not Stack B)
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    // Match "E2E Test Stack" but not "E2E Test Stack B" (negative lookahead)
    const productCardA2 = page.locator('div').filter({ hasText: /^E2E Test Stack(?! B)/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCardA2.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    let appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
    let appPortValue = await page.locator('text=Application Port').locator('..').locator('input').inputValue();

    expect(appNameValue).toBe('stack-a-app');
    expect(appPortValue).toBe('9004');

    console.log('✓ Stack A preserved its own values');

    // Redeploy Stack B and verify it has Stack B values (not Stack A)
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    const productCardB2 = page.locator('div').filter({ hasText: /^E2E Test Stack B/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCardB2.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
    appPortValue = await page.locator('text=Application Port').locator('..').locator('input').inputValue();

    expect(appNameValue).toBe('stack-b-app');
    expect(appPortValue).toBe('9005');

    console.log('✓ Stack B preserved its own values - product isolation confirmed');
  });

  // TODO: Boolean toggle selector needs investigation - UI structure may differ
  test('should persist different variable types correctly', async ({ page }) => {
    // Navigate to catalog and find Variable Types Test Stack
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    await expect(page.getByText('Variable Types Test Stack').first()).toBeVisible({ timeout: 10000 });

    const productCard = page.locator('div').filter({ hasText: /^Variable Types Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Fill in stack name and various variable types
    const stackName = `e2e-types-${Date.now()}`;
    await page.locator('text=Stack Name').locator('..').locator('input').fill(stackName);

    // Boolean toggle - find by label text (component uses button[type="button"], not role="switch")
    await page.waitForTimeout(500);
    // Navigate from label -> parent div -> parent flex div -> find button sibling
    const boolToggle = page.locator('text=Enable Feature').locator('..').locator('..').locator('button[type="button"]');
    await expect(boolToggle).toBeVisible();
    // Check if toggle is off (bg-gray class indicates false state) and turn it on
    const classes = await boolToggle.getAttribute('class');
    const isOff = classes?.includes('bg-gray');
    if (isOff) {
      await boolToggle.click();
      await page.waitForTimeout(500);
      // Verify it switched to on state
      const classesAfter = await boolToggle.getAttribute('class');
      const isNowOn = classesAfter?.includes('bg-brand');
      console.log(`✓ Toggle state after click: ${isNowOn ? 'ON' : 'OFF'} (classes: ${classesAfter})`);
    }

    // Number input
    const maxConnInput = page.locator('label:has-text("Max Connections")').locator('..').locator('input[type="number"], input[type="text"]');
    await maxConnInput.clear();
    await maxConnInput.fill('100');

    // Select dropdown
    const logLevelSelect = page.locator('label:has-text("Log Level")').locator('..').locator('select');
    await logLevelSelect.selectOption('debug');

    // MultiLine textarea
    const descTextarea = page.locator('label:has-text("Description")').locator('..').locator('textarea');
    await descTextarea.clear();
    await descTextarea.fill('Line 1\nLine 2\nLine 3');

    await page.getByRole('button', { name: /deploy to/i }).click();
    await expect(page.getByText(/stack deployed successfully/i)).toBeVisible({ timeout: 120000 });

    console.log('✓ Deployed with various variable types');

    // Redeploy to test auto-load
    await page.goto('/catalog');
    await page.waitForTimeout(1000);

    const productCard2 = page.locator('div').filter({ hasText: /^Variable Types Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard2.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Verify all variable types were persisted correctly
    const boolToggle2 = page.locator('text=Enable Feature').locator('..').locator('..').locator('button[type="button"]');
    await expect(boolToggle2).toBeVisible();
    const classes2 = await boolToggle2.getAttribute('class');
    console.log(`✓ Toggle classes on reload: ${classes2}`);
    const isOn = classes2?.includes('bg-brand');
    console.log(`✓ Toggle state verification: ${isOn ? 'ON (persisted correctly)' : 'OFF (persistence FAILED)'}`);
    expect(isOn).toBe(true);

    const numberValue = await page.locator('label:has-text("Max Connections")').locator('..').locator('input[type="number"], input[type="text"]').inputValue();
    expect(numberValue).toBe('100');

    const selectValue = await page.locator('label:has-text("Log Level")').locator('..').locator('select').inputValue();
    expect(selectValue).toBe('debug');

    const textareaValue = await page.locator('label:has-text("Description")').locator('..').locator('textarea').inputValue();
    expect(textareaValue).toBe('Line 1\nLine 2\nLine 3');

    console.log('✓ All variable types persisted correctly: Boolean, Number, Select, MultiLine');
  });

  test('should import .env file and populate variables', async ({ page }) => {
    // Navigate to catalog and deploy test stack
    await page.goto('/catalog');
    await page.waitForTimeout(2000);

    await expect(page.getByText('E2E Test Stack').first()).toBeVisible({ timeout: 10000 });

    const productCard = page.locator('div').filter({ hasText: /^E2E Test Stack/ }).filter({ has: page.getByRole('link', { name: /view details/i }) }).first();
    await productCard.getByRole('link', { name: /view details/i }).click();
    await page.waitForTimeout(1000);

    await page.getByRole('link', { name: 'Deploy', exact: true }).click();
    await expect(page).toHaveURL(/\/deploy\//);
    await page.waitForLoadState('networkidle');

    // Reset to defaults first to ensure clean state (product isolation bug causes cross-contamination)
    await page.getByRole('button', { name: /reset to defaults/i }).click();
    await page.waitForTimeout(500);

    // Create a .env file content
    const envContent = `APP_NAME=imported-app
APP_PORT=7777
DATABASE_URL="postgres://imported-server:5432/imported-db"
API_KEY='imported-secret-key'
# This is a comment
UNKNOWN_VAR=ignored
`;

    // Create a temporary .env file for testing
    const tmpDir = os.tmpdir();
    const envFilePath = path.join(tmpDir, `test-${Date.now()}.env`);
    fs.writeFileSync(envFilePath, envContent);

    try {
      // Locate the file input and upload the .env file
      const fileInput = page.locator('input[type="file"]').first();
      await fileInput.setInputFiles(envFilePath);

      // Wait a moment for file processing
      await page.waitForTimeout(1000);

      // Verify variables were populated from .env file
      const appNameValue = await page.locator('text=Application Name').locator('..').locator('input').inputValue();
      const appPortValue = await page.locator('text=Application Port').locator('..').locator('input').inputValue();
      const dbUrlValue = await page.locator('text=Database URL').locator('..').locator('input').inputValue();
      const apiKeyValue = await page.locator('text=API Key').locator('..').locator('input').inputValue();

      expect(appNameValue).toBe('imported-app');
      expect(appPortValue).toBe('7777');
      expect(dbUrlValue).toBe('postgres://imported-server:5432/imported-db');
      expect(apiKeyValue).toBe('imported-secret-key');

      console.log('✓ .env file imported and variables populated correctly');
    } finally {
      // Clean up temp file
      fs.unlinkSync(envFilePath);
    }
  });

  test.skip('should isolate variables between different environments', async ({ page }) => {
    // This test would require creating multiple environments
    // Skipped for now as it requires more complex setup
    await page.goto('/environments');
    console.log('Test skipped - requires multiple environments');
  });
});
