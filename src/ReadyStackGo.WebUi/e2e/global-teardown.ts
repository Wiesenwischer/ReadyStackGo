import { execSync } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * Global teardown for Playwright E2E tests
 * Cleans up Docker containers and volumes after all tests complete
 */
async function globalTeardown() {
  const repoRoot = path.resolve(__dirname, '..', '..', '..');
  const composeFile = path.join(repoRoot, 'docker-compose.e2e.yml');

  console.log('Tearing down E2E Docker environment...');

  try {
    execSync(`docker compose -f "${composeFile}" down -v --remove-orphans`, {
      stdio: 'inherit',
      cwd: repoRoot
    });
    console.log('E2E environment cleaned up successfully');
  } catch (error) {
    console.warn(`Warning: Could not tear down E2E environment: ${error}`);
  }
}

export default globalTeardown;
