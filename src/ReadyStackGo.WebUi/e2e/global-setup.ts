import type { FullConfig } from '@playwright/test';
import { execSync } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * Global setup for Playwright E2E tests
 * Starts fresh Docker environment for tests
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
async function globalSetup(_config: FullConfig) {
  const repoRoot = path.resolve(__dirname, '..', '..', '..');
  const composeFile = path.join(repoRoot, 'docker-compose.e2e.yml');

  console.log('Starting E2E Docker environment...');

  try {
    // Stop and remove any existing E2E containers and volumes
    execSync(`docker compose -f "${composeFile}" down -v --remove-orphans`, {
      stdio: 'inherit',
      cwd: repoRoot
    });

    // Start fresh E2E environment
    execSync(`docker compose -f "${composeFile}" up --build -d`, {
      stdio: 'inherit',
      cwd: repoRoot
    });

    // Wait for container to be healthy
    console.log('Waiting for backend to be ready...');
    for (let i = 0; i < 60; i++) {
      try {
        execSync('curl -f http://localhost:5259/health', { stdio: 'ignore' });
        console.log('Backend is ready!');
        break;
      } catch {
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
      if (i === 59) {
        throw new Error('Backend failed to start within 60 seconds');
      }
    }

    // Create admin user via wizard endpoint
    console.log('Creating E2E test admin user...');
    const createAdminResult = execSync(
      'curl -X POST http://localhost:5259/api/wizard/admin ' +
      '-H "Content-Type: application/json" ' +
      '-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}" ' +
      '-w "\\n%{http_code}" -s',
      { encoding: 'utf-8' }
    );

    const lines = createAdminResult.trim().split('\n');
    const httpCode = lines[lines.length - 1];
    const responseBody = lines.slice(0, -1).join('\n');

    if (httpCode === '200' || httpCode === '201') {
      console.log('✓ Admin user created successfully');
    } else {
      console.error(`✗ Failed to create admin user (HTTP ${httpCode})`);
      console.error(`Response: ${responseBody}`);
      throw new Error(`Failed to create E2E admin user: HTTP ${httpCode}`);
    }

    // Complete wizard: Create organization
    console.log('Creating test organization...');
    const createOrgResult = execSync(
      'curl -X POST http://localhost:5259/api/wizard/organization ' +
      '-H "Content-Type: application/json" ' +
      '-d "{\\"id\\":\\"e2e-test-org\\",\\"name\\":\\"E2E Test Organization\\"}" ' +
      '-w "\\n%{http_code}" -s',
      { encoding: 'utf-8' }
    );

    const orgLines = createOrgResult.trim().split('\n');
    const orgHttpCode = orgLines[orgLines.length - 1];
    const orgResponseBody = orgLines.slice(0, -1).join('\n');

    if (orgHttpCode === '200' || orgHttpCode === '201') {
      console.log('✓ Organization created successfully');
    } else {
      console.error(`✗ Failed to create organization (HTTP ${orgHttpCode})`);
      console.error(`Response: ${orgResponseBody}`);
      throw new Error(`Failed to create organization: HTTP ${orgHttpCode}`);
    }

    // Complete wizard: Create environment
    console.log('Creating test environment...');
    const createEnvResult = execSync(
      'curl -X POST http://localhost:5259/api/wizard/environment ' +
      '-H "Content-Type: application/json" ' +
      '-d "{\\"name\\":\\"default\\",\\"socketPath\\":\\"/var/run/docker.sock\\"}" ' +
      '-w "\\n%{http_code}" -s',
      { encoding: 'utf-8' }
    );

    const envLines = createEnvResult.trim().split('\n');
    const envHttpCode = envLines[envLines.length - 1];
    const envResponseBody = envLines.slice(0, -1).join('\n');

    if (envHttpCode === '200' || envHttpCode === '201') {
      console.log('✓ Environment created successfully');
    } else {
      console.error(`✗ Failed to create environment (HTTP ${envHttpCode})`);
      console.error(`Response: ${envResponseBody}`);
      throw new Error(`Failed to create environment: HTTP ${envHttpCode}`);
    }

    // Complete wizard
    console.log('Completing wizard...');
    const completeWizardResult = execSync(
      'curl -X POST http://localhost:5259/api/wizard/install ' +
      '-H "Content-Type: application/json" ' +
      '-d "{}" ' +
      '-w "\\n%{http_code}" -s',
      { encoding: 'utf-8' }
    );

    const completeLines = completeWizardResult.trim().split('\n');
    const completeHttpCode = completeLines[completeLines.length - 1];
    const completeResponseBody = completeLines.slice(0, -1).join('\n');

    if (completeHttpCode === '200' || completeHttpCode === '201') {
      console.log('✓ Wizard completed successfully');
    } else {
      console.error(`✗ Failed to complete wizard (HTTP ${completeHttpCode})`);
      console.error(`Response: ${completeResponseBody}`);
      throw new Error(`Failed to complete wizard: HTTP ${completeHttpCode}`);
    }

    console.log('✓ Wizard setup complete!');

    // Login to get auth token for authenticated API calls
    console.log('Logging in to get auth token...');
    const loginResult = execSync(
      'curl -X POST http://localhost:5259/api/auth/login ' +
      '-H "Content-Type: application/json" ' +
      '-d "{\\"username\\":\\"admin\\",\\"password\\":\\"Admin1234\\"}" ' +
      '-w "\\n%{http_code}" -s',
      { encoding: 'utf-8' }
    );

    const loginLines = loginResult.trim().split('\n');
    const loginHttpCode = loginLines[loginLines.length - 1];
    const loginResponseBody = loginLines.slice(0, -1).join('\n');

    if (loginHttpCode !== '200') {
      console.error(`✗ Failed to login (HTTP ${loginHttpCode})`);
      console.error(`Response: ${loginResponseBody}`);
      throw new Error(`Failed to login: HTTP ${loginHttpCode}`);
    }

    const loginResponse = JSON.parse(loginResponseBody);
    const authToken = loginResponse.token;
    console.log('✓ Logged in successfully');

    // Configure test stack source for E2E tests
    console.log('Configuring test stack source...');
    // Use the container path where test stacks are mounted
    const testStacksPath = '/app/test-stacks';
    const createSourceResult = execSync(
      'curl -X POST http://localhost:5259/api/stack-sources ' +
      '-H "Content-Type: application/json" ' +
      `-H "Authorization: Bearer ${authToken}" ` +
      `-d "{\\"id\\":\\"e2e-test-source\\",\\"name\\":\\"E2E Test Stacks\\",\\"type\\":\\"LocalDirectory\\",\\"path\\":\\"${testStacksPath}\\",\\"filePattern\\":\\"*\\"}" ` +
      '-w "\\n%{http_code}" -s',
      { encoding: 'utf-8' }
    );

    const sourceLines = createSourceResult.trim().split('\n');
    const sourceHttpCode = sourceLines[sourceLines.length - 1];
    const sourceResponseBody = sourceLines.slice(0, -1).join('\n');

    if (sourceHttpCode === '200' || sourceHttpCode === '201') {
      console.log('✓ Test stack source created successfully');

      // Sync sources to load stacks
      console.log('Syncing stack sources...');
      const syncResult = execSync(
        'curl -X POST http://localhost:5259/api/stack-sources/sync ' +
        `-H "Authorization: Bearer ${authToken}" ` +
        '-w "\\n%{http_code}" -s',
        { encoding: 'utf-8' }
      );

      const syncLines = syncResult.trim().split('\n');
      const syncHttpCode = syncLines[syncLines.length - 1];

      if (syncHttpCode === '200' || syncHttpCode === '202') {
        console.log('✓ Stack sources synced');
      } else {
        console.warn(`⚠ Stack sync returned HTTP ${syncHttpCode}`);
      }
    } else {
      console.error(`✗ Could not create test stack source (HTTP ${sourceHttpCode})`);
      console.error(`Response: ${sourceResponseBody}`);
      console.warn(`⚠ Tests may fail without test stack source`);
    }

    console.log('E2E environment ready!');
  } catch (error) {
    console.error(`Error starting E2E environment: ${error}`);
    throw error;
  }
}

export default globalSetup;
