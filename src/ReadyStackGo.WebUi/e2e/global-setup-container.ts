import { FullConfig } from '@playwright/test';

/**
 * Global setup for Container E2E tests
 * The container should already be running with its own config volume mounted.
 * This setup just waits for the API to be ready.
 */
async function globalSetup(config: FullConfig) {
  const baseURL = process.env.E2E_BASE_URL || 'http://localhost:8080';
  const maxRetries = 30; // 30 * 2s = 60s timeout
  const retryDelay = 2000;

  console.log(`Waiting for container API at ${baseURL}/health...`);

  for (let i = 0; i < maxRetries; i++) {
    try {
      const response = await fetch(`${baseURL}/health`);
      if (response.ok) {
        console.log('Container API is ready!');
        return;
      }
    } catch (error) {
      // Container not ready yet, retry
    }

    await new Promise(resolve => setTimeout(resolve, retryDelay));
    process.stdout.write('.');
  }

  throw new Error(`Container API at ${baseURL} did not become ready within timeout`);
}

export default globalSetup;
