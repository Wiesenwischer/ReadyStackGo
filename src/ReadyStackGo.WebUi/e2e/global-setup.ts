import type { FullConfig } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * Global setup for Playwright E2E tests
 * Copies test fixtures to the API config directory
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
async function globalSetup(_config: FullConfig) {
  const fixturesDir = path.join(__dirname, 'fixtures', 'config');
  const configDir = path.join(__dirname, '..', '..', 'ReadyStackGo.Api', 'config');

  // Create config directory if it doesn't exist
  if (!fs.existsSync(configDir)) {
    fs.mkdirSync(configDir, { recursive: true });
  }

  // Copy test fixtures to API config directory
  const files = ['rsgo.system.json', 'rsgo.security.json'];

  for (const file of files) {
    const src = path.join(fixturesDir, file);
    const dest = path.join(configDir, file);

    if (fs.existsSync(src)) {
      fs.copyFileSync(src, dest);
      console.log(`Copied ${file} to ${configDir}`);
    } else {
      console.warn(`Warning: ${src} not found`);
    }
  }

  console.log('E2E test environment configured');
}

export default globalSetup;
