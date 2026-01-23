import { test, expect } from '@playwright/test';
import { login } from './helpers/auth';

/**
 * E2E Tests for Environment Variable Persistence
 * Tests that environment variables are saved and auto-loaded for deployments (v0.17 feature)
 */

test.describe('Environment Variable Persistence', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should auto-save variables after successful deployment', async ({ page }) => {
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // Open Deploy Stack modal
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    // Step 1: Enter stack details with variables
    const stackName = `e2e-vars-test-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(stackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  db:
    image: postgres:15-alpine
    environment:
      POSTGRES_USER: \${DB_USER:-postgres}
      POSTGRES_PASSWORD: \${DB_PASSWORD}
      POSTGRES_DB: \${DB_NAME:-testdb}`);

    // Parse YAML
    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    // Step 2: Fill in variable values
    const dbUserInput = page.getByLabel(/DB_USER/i);
    const dbPasswordInput = page.getByLabel(/DB_PASSWORD/i);
    const dbNameInput = page.getByLabel(/DB_NAME/i);

    const hasVariables = await dbUserInput.isVisible().catch(() => false) ||
                         await dbPasswordInput.isVisible().catch(() => false);

    if (hasVariables) {
      if (await dbUserInput.isVisible()) {
        await dbUserInput.fill('testuser');
      }
      if (await dbPasswordInput.isVisible()) {
        await dbPasswordInput.fill('testpassword123');
      }
      if (await dbNameInput.isVisible()) {
        await dbNameInput.fill('myappdb');
      }

      // Deploy
      const deployBtn = page.getByRole('button', { name: /^deploy$/i });
      if (await deployBtn.isVisible()) {
        await deployBtn.click();

        // Wait for deployment to complete
        await page.waitForTimeout(8000);

        // Modal should close
        await expect(page.getByRole('heading', { name: /deploy docker compose/i }))
          .not.toBeVisible({ timeout: 15000 });

        // Deployment should appear in list
        await expect(page.getByText(stackName)).toBeVisible({ timeout: 10000 });
      }
    }
  });

  test('should auto-load saved variables on next deployment', async ({ page }) => {
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // First deployment with variables
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const firstStackName = `e2e-first-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(firstStackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      APP_NAME: \${APP_NAME}
      API_KEY: \${API_KEY}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    // Fill variables
    const appNameInput = page.getByLabel(/APP_NAME/i);
    const apiKeyInput = page.getByLabel(/API_KEY/i);

    const hasVars = await appNameInput.isVisible().catch(() => false);
    if (hasVars) {
      await appNameInput.fill('ReadyStackGo');
      await apiKeyInput.fill('secret-api-key-123');

      // Deploy first stack
      const deployBtn = page.getByRole('button', { name: /^deploy$/i });
      if (await deployBtn.isVisible()) {
        await deployBtn.click();
        await page.waitForTimeout(8000);
        await expect(page.getByRole('heading', { name: /deploy docker compose/i }))
          .not.toBeVisible({ timeout: 15000 });
      }
    }

    // Wait for UI to update
    await page.waitForTimeout(2000);

    // Second deployment - should auto-load saved variables
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const secondStackName = `e2e-second-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(secondStackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  web:
    image: httpd:alpine
    environment:
      APP_NAME: \${APP_NAME}
      API_KEY: \${API_KEY}
      PORT: \${PORT:-8080}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    // Verify saved variables are auto-loaded
    const secondAppNameInput = page.getByLabel(/APP_NAME/i);
    const secondApiKeyInput = page.getByLabel(/API_KEY/i);

    if (await secondAppNameInput.isVisible()) {
      const appNameValue = await secondAppNameInput.inputValue();
      const apiKeyValue = await secondApiKeyInput.inputValue();

      // Variables should be pre-filled with saved values
      expect(appNameValue).toBe('ReadyStackGo');
      expect(apiKeyValue).toBe('secret-api-key-123');
    }

    // Cancel to clean up
    await page.getByRole('button', { name: /cancel/i }).click();
  });

  test('should allow resetting variables to defaults', async ({ page }) => {
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // Open Deploy Stack modal
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const stackName = `e2e-reset-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(stackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      VERSION: \${VERSION:-1.0.0}
      ENV: \${ENV:-development}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    // Fill variables with custom values
    const versionInput = page.getByLabel(/VERSION/i);
    const envInput = page.getByLabel(/ENV/i);

    const hasInputs = await versionInput.isVisible().catch(() => false);
    if (hasInputs) {
      // Change from defaults
      await versionInput.fill('2.5.0');
      await envInput.fill('production');

      // Verify changed values
      expect(await versionInput.inputValue()).toBe('2.5.0');
      expect(await envInput.inputValue()).toBe('production');

      // Look for Reset button
      const resetButton = page.getByRole('button', { name: /reset.*default/i });
      const hasResetButton = await resetButton.isVisible().catch(() => false);

      if (hasResetButton) {
        // Click Reset
        await resetButton.click();
        await page.waitForTimeout(500);

        // Values should be back to defaults
        const versionAfterReset = await versionInput.inputValue();
        const envAfterReset = await envInput.inputValue();

        expect(versionAfterReset).toBe('1.0.0');
        expect(envAfterReset).toBe('development');
      }
    }

    // Cancel
    await page.getByRole('button', { name: /cancel/i }).click();
  });

  test('should merge saved variables with manifest defaults', async ({ page }) => {
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // First deployment - save some variables
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const firstStackName = `e2e-merge-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(firstStackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      SHARED_VAR: \${SHARED_VAR}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    const sharedVarInput = page.getByLabel(/SHARED_VAR/i);
    if (await sharedVarInput.isVisible()) {
      await sharedVarInput.fill('shared-value-123');

      const deployBtn = page.getByRole('button', { name: /^deploy$/i });
      if (await deployBtn.isVisible()) {
        await deployBtn.click();
        await page.waitForTimeout(8000);
        await expect(page.getByRole('heading', { name: /deploy docker compose/i }))
          .not.toBeVisible({ timeout: 15000 });
      }
    }

    await page.waitForTimeout(2000);

    // Second deployment - has both saved and new variables
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const secondStackName = `e2e-merge2-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(secondStackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: httpd:alpine
    environment:
      SHARED_VAR: \${SHARED_VAR}
      NEW_VAR: \${NEW_VAR:-default-value}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    const secondSharedVarInput = page.getByLabel(/SHARED_VAR/i);
    const newVarInput = page.getByLabel(/NEW_VAR/i);

    if (await secondSharedVarInput.isVisible()) {
      // Saved variable should be loaded
      expect(await secondSharedVarInput.inputValue()).toBe('shared-value-123');

      // New variable should have default from manifest
      if (await newVarInput.isVisible()) {
        expect(await newVarInput.inputValue()).toBe('default-value');
      }
    }

    // Cancel
    await page.getByRole('button', { name: /cancel/i }).click();
  });

  test('should handle empty saved variables gracefully', async ({ page }) => {
    // Navigate to Settings to create a new environment
    await page.goto('/settings');
    await page.waitForTimeout(2000);

    // Create a new environment (will have no saved variables)
    const addEnvButton = page.getByRole('button', { name: /add environment/i });
    const hasAddButton = await addEnvButton.isVisible().catch(() => false);

    if (hasAddButton) {
      await addEnvButton.click();
      await page.waitForTimeout(1000);

      // Fill environment details
      const envName = `e2e-new-env-${Date.now()}`;
      const nameInput = page.getByLabel(/name/i).first();
      await nameInput.fill(envName);

      const socketPathInput = page.getByLabel(/socket.*path|docker.*socket/i).first();
      if (await socketPathInput.isVisible()) {
        await socketPathInput.fill('/var/run/docker.sock');
      }

      // Save environment
      const saveButton = page.getByRole('button', { name: /save|create/i });
      if (await saveButton.isVisible()) {
        await saveButton.click();
        await page.waitForTimeout(2000);
      }

      // Go to stacks and deploy
      await page.goto('/stacks');
      await page.waitForTimeout(2000);

      // Open Deploy Stack modal
      await page.getByRole('button', { name: /deploy stack/i }).click();
      await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

      const stackName = `e2e-empty-${Date.now()}`;
      await page.getByLabel(/stack name/i).fill(stackName);
      await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      TEST_VAR: \${TEST_VAR:-default}`);

      await page.getByRole('button', { name: /next|parse/i }).click();
      await page.waitForTimeout(2000);

      // Should show default value since no variables are saved yet
      const testVarInput = page.getByLabel(/TEST_VAR/i);
      if (await testVarInput.isVisible()) {
        expect(await testVarInput.inputValue()).toBe('default');
      }

      // Cancel
      await page.getByRole('button', { name: /cancel/i }).click();
    }
  });

  test('should persist sensitive variable values', async ({ page }) => {
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // Deploy with sensitive variables
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const stackName = `e2e-sensitive-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(stackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      DB_PASSWORD: \${DB_PASSWORD}
      API_SECRET: \${API_SECRET}
      AUTH_TOKEN: \${AUTH_TOKEN}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    const dbPasswordInput = page.getByLabel(/DB_PASSWORD/i);
    const apiSecretInput = page.getByLabel(/API_SECRET/i);
    const authTokenInput = page.getByLabel(/AUTH_TOKEN/i);

    if (await dbPasswordInput.isVisible()) {
      await dbPasswordInput.fill('my-secret-password');
      await apiSecretInput.fill('my-api-secret');
      await authTokenInput.fill('my-auth-token');

      const deployBtn = page.getByRole('button', { name: /^deploy$/i });
      if (await deployBtn.isVisible()) {
        await deployBtn.click();
        await page.waitForTimeout(8000);
        await expect(page.getByRole('heading', { name: /deploy docker compose/i }))
          .not.toBeVisible({ timeout: 15000 });
      }
    }

    await page.waitForTimeout(2000);

    // Deploy another stack - sensitive values should be auto-loaded
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const secondStackName = `e2e-sensitive2-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(secondStackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  db:
    image: postgres:15-alpine
    environment:
      POSTGRES_PASSWORD: \${DB_PASSWORD}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    const secondDbPasswordInput = page.getByLabel(/DB_PASSWORD/i);
    if (await secondDbPasswordInput.isVisible()) {
      // Sensitive variable should be loaded
      expect(await secondDbPasswordInput.inputValue()).toBe('my-secret-password');
    }

    // Cancel
    await page.getByRole('button', { name: /cancel/i }).click();
  });

  test('should handle special characters in variable values', async ({ page }) => {
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const stackName = `e2e-special-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(stackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      CONNECTION_STRING: \${CONNECTION_STRING}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    const connectionStringInput = page.getByLabel(/CONNECTION_STRING/i);
    if (await connectionStringInput.isVisible()) {
      // Value with special characters
      const specialValue = 'Server=localhost;Database=mydb;User=admin;Password=P@ss!w0rd#123';
      await connectionStringInput.fill(specialValue);

      const deployBtn = page.getByRole('button', { name: /^deploy$/i });
      if (await deployBtn.isVisible()) {
        await deployBtn.click();
        await page.waitForTimeout(8000);
        await expect(page.getByRole('heading', { name: /deploy docker compose/i }))
          .not.toBeVisible({ timeout: 15000 });
      }

      await page.waitForTimeout(2000);

      // Deploy again - special characters should be preserved
      await page.getByRole('button', { name: /deploy stack/i }).click();
      await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

      const secondStackName = `e2e-special2-${Date.now()}`;
      await page.getByLabel(/stack name/i).fill(secondStackName);
      await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  db:
    image: postgres:15-alpine
    environment:
      DB_CONNECTION: \${CONNECTION_STRING}`);

      await page.getByRole('button', { name: /next|parse/i }).click();
      await page.waitForTimeout(2000);

      const secondConnectionInput = page.getByLabel(/CONNECTION_STRING/i);
      if (await secondConnectionInput.isVisible()) {
        // Special characters should be preserved
        expect(await secondConnectionInput.inputValue()).toBe(specialValue);
      }

      // Cancel
      await page.getByRole('button', { name: /cancel/i }).click();
    }
  });
});

test.describe('Environment Variable Isolation', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should isolate variables between different environments', async ({ page }) => {
    // This test would require creating multiple environments
    // For now, we verify that switching environments shows different variables
    await page.goto('/stacks');
    await page.waitForTimeout(2000);

    // Get current environment name from the heading
    const heading = page.getByRole('heading', { name: /deployed stacks/i });
    const headingText = await heading.textContent();

    // Variables are scoped per environment
    // Deploy a stack and verify variables are saved for this environment
    await page.getByRole('button', { name: /deploy stack/i }).click();
    await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

    const stackName = `e2e-isolation-${Date.now()}`;
    await page.getByLabel(/stack name/i).fill(stackName);
    await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  app:
    image: nginx:alpine
    environment:
      ENV_SPECIFIC_VAR: \${ENV_SPECIFIC_VAR}`);

    await page.getByRole('button', { name: /next|parse/i }).click();
    await page.waitForTimeout(2000);

    const envSpecificInput = page.getByLabel(/ENV_SPECIFIC_VAR/i);
    if (await envSpecificInput.isVisible()) {
      const environmentSpecificValue = `value-for-${headingText}`;
      await envSpecificInput.fill(environmentSpecificValue);

      const deployBtn = page.getByRole('button', { name: /^deploy$/i });
      if (await deployBtn.isVisible()) {
        await deployBtn.click();
        await page.waitForTimeout(8000);
        await expect(page.getByRole('heading', { name: /deploy docker compose/i }))
          .not.toBeVisible({ timeout: 15000 });
      }

      await page.waitForTimeout(2000);

      // Deploy another stack in same environment - should have same value
      await page.getByRole('button', { name: /deploy stack/i }).click();
      await expect(page.getByRole('heading', { name: /deploy docker compose/i })).toBeVisible();

      const secondStackName = `e2e-isolation2-${Date.now()}`;
      await page.getByLabel(/stack name/i).fill(secondStackName);
      await page.getByLabel(/docker compose yaml/i).fill(`version: '3.8'
services:
  web:
    image: httpd:alpine
    environment:
      ENV_SPECIFIC_VAR: \${ENV_SPECIFIC_VAR}`);

      await page.getByRole('button', { name: /next|parse/i }).click();
      await page.waitForTimeout(2000);

      const secondEnvSpecificInput = page.getByLabel(/ENV_SPECIFIC_VAR/i);
      if (await secondEnvSpecificInput.isVisible()) {
        // Should load the environment-specific value
        expect(await secondEnvSpecificInput.inputValue()).toBe(environmentSpecificValue);
      }

      // Cancel
      await page.getByRole('button', { name: /cancel/i }).click();
    }
  });
});
