import type { FullConfig } from '@playwright/test';
import { execSync } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const BASE_URL = 'http://localhost:8080';

/**
 * Global setup for Playwright E2E tests.
 * Builds and starts the Docker container, then completes the setup wizard.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
async function globalSetup(_config: FullConfig) {
  const repoRoot = path.resolve(__dirname, '..', '..', '..');

  console.log('Starting E2E Docker environment...');

  try {
    // Stop and remove any existing containers and volumes for a clean start
    execSync('docker compose down -v', {
      stdio: 'inherit',
      cwd: repoRoot
    });

    // Build and start container
    execSync('docker compose up --build -d', {
      stdio: 'inherit',
      cwd: repoRoot
    });

    // Wait for container to be healthy
    console.log('Waiting for backend to be ready...');
    for (let i = 0; i < 60; i++) {
      try {
        const result = execSync(`curl -sf ${BASE_URL}/api/wizard/status`, {
          encoding: 'utf-8',
          stdio: ['pipe', 'pipe', 'ignore']
        });
        if (result.includes('wizardState')) {
          console.log('Backend is ready!');
          break;
        }
      } catch {
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
      if (i === 59) {
        throw new Error('Backend failed to start within 60 seconds');
      }
    }

    // Create admin user via wizard endpoint
    console.log('Creating E2E test admin user...');
    execApiCall(
      `${BASE_URL}/api/wizard/admin`,
      '{"username":"admin","password":"Admin1234"}',
      'Admin user'
    );

    // Create organization
    console.log('Creating test organization...');
    execApiCall(
      `${BASE_URL}/api/wizard/organization`,
      '{"id":"e2e-test-org","name":"E2E Test Organization"}',
      'Organization'
    );

    // Create environment
    console.log('Creating test environment...');
    execApiCall(
      `${BASE_URL}/api/wizard/environment`,
      '{"name":"default","socketPath":"/var/run/docker.sock"}',
      'Environment'
    );

    // Complete wizard
    console.log('Completing wizard...');
    execApiCall(
      `${BASE_URL}/api/wizard/install`,
      '{}',
      'Wizard completion'
    );

    console.log('✓ Wizard setup complete!');

    // Login to get auth token for stack source configuration
    console.log('Logging in to get auth token...');
    const loginResult = execSync(
      `curl -sf -X POST ${BASE_URL}/api/auth/login ` +
      '-H "Content-Type: application/json" ' +
      '-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}"',
      { encoding: 'utf-8' }
    );

    const loginResponse = JSON.parse(loginResult);
    const authToken = loginResponse.token;
    console.log('✓ Logged in successfully');

    // Configure test stack source
    console.log('Configuring test stack source...');
    try {
      execSync(
        `curl -sf -X POST ${BASE_URL}/api/stack-sources ` +
        '-H "Content-Type: application/json" ' +
        `-H "Authorization: Bearer ${authToken}" ` +
        '-d "{\\"id\\":\\"e2e-test-source\\",\\"name\\":\\"E2E Test Stacks\\",\\"type\\":\\"LocalDirectory\\",\\"path\\":\\"/app/stacks\\",\\"filePattern\\":\\"*\\"}"',
        { encoding: 'utf-8' }
      );
      console.log('✓ Test stack source created');

      // Sync sources
      execSync(
        `curl -sf -X POST ${BASE_URL}/api/stack-sources/sync ` +
        `-H "Authorization: Bearer ${authToken}"`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'ignore'] }
      );
      console.log('✓ Stack sources synced');
    } catch {
      console.warn('⚠ Could not create test stack source (may already exist)');
    }

    console.log('E2E environment ready!');
  } catch (error) {
    console.error(`Error starting E2E environment: ${error}`);
    throw error;
  }
}

function execApiCall(url: string, body: string, label: string) {
  const result = execSync(
    `curl -sf -X POST ${url} ` +
    '-H "Content-Type: application/json" ' +
    `-d "${body.replace(/"/g, '\\"')}"` +
    ' -w "\\n%{http_code}"',
    { encoding: 'utf-8' }
  );

  const lines = result.trim().split('\n');
  const httpCode = lines[lines.length - 1];

  if (httpCode === '200' || httpCode === '201') {
    console.log(`✓ ${label} created successfully`);
  } else {
    throw new Error(`Failed to create ${label}: HTTP ${httpCode}`);
  }
}

export default globalSetup;
