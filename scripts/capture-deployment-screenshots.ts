/**
 * Playwright script to capture screenshots for deployment documentation
 * Run from WebUi folder: npx tsx ../../scripts/capture-deployment-screenshots.ts
 */
import { chromium } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const BASE_URL = process.env.RSGO_URL || 'http://localhost:8080';
const USERNAME = process.env.RSGO_USERNAME || 'admin';
const PASSWORD = process.env.RSGO_PASSWORD || '';
const OUTPUT_DIR = path.join(__dirname, '..', 'docs', 'images', 'deployment');

async function main() {
  // Ensure output directory exists
  if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  }

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext({
    viewport: { width: 1400, height: 900 }
  });
  const page = await context.newPage();

  try {
    console.log('1. Navigating to login page...');
    await page.goto(BASE_URL);
    await page.waitForLoadState('networkidle');

    // Screenshot: Login page
    await page.screenshot({
      path: path.join(OUTPUT_DIR, '01-login.png'),
      fullPage: false
    });
    console.log('   Captured: 01-login.png');

    console.log('2. Logging in...');
    await page.fill('input[name="username"], input[type="text"]', USERNAME);
    await page.fill('input[name="password"], input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await page.waitForURL('**/dashboard', { timeout: 10000 }).catch(() => {});
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Screenshot: Dashboard
    await page.screenshot({
      path: path.join(OUTPUT_DIR, '02-dashboard.png'),
      fullPage: false
    });
    console.log('   Captured: 02-dashboard.png');

    console.log('3. Navigating to Stack Catalog...');
    await page.click('a[href="/catalog"], text=Catalog');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Screenshot: Stack Catalog
    await page.screenshot({
      path: path.join(OUTPUT_DIR, '03-catalog.png'),
      fullPage: false
    });
    console.log('   Captured: 03-catalog.png');

    console.log('4. Selecting a product...');
    // Click on first product card
    const productCard = page.locator('a[href*="/catalog/"]').first();
    if (await productCard.isVisible()) {
      await productCard.click();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(1000);

      // Screenshot: Product Detail
      await page.screenshot({
        path: path.join(OUTPUT_DIR, '04-product-detail.png'),
        fullPage: false
      });
      console.log('   Captured: 04-product-detail.png');

      console.log('5. Clicking Deploy button...');
      const deployButton = page.locator('a:has-text("Deploy")').first();
      if (await deployButton.isVisible()) {
        await deployButton.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(1000);

        // Screenshot: Deploy page - Configuration
        await page.screenshot({
          path: path.join(OUTPUT_DIR, '05-deploy-configure.png'),
          fullPage: false
        });
        console.log('   Captured: 05-deploy-configure.png');

        // Screenshot: Deploy page - Focus on Import .env button (if visible)
        const importButton = page.locator('label:has-text("Import .env")');
        if (await importButton.isVisible()) {
          await importButton.scrollIntoViewIfNeeded();
          await page.screenshot({
            path: path.join(OUTPUT_DIR, '06-deploy-import-env-button.png'),
            fullPage: false
          });
          console.log('   Captured: 06-deploy-import-env-button.png');
        }

        // Screenshot: Variables section
        const variablesSection = page.locator('text=Environment Variables').first();
        if (await variablesSection.isVisible()) {
          await variablesSection.scrollIntoViewIfNeeded();
          await page.waitForTimeout(500);
          await page.screenshot({
            path: path.join(OUTPUT_DIR, '07-deploy-variables.png'),
            fullPage: false
          });
          console.log('   Captured: 07-deploy-variables.png');
        }
      }
    }

    console.log('6. Navigating to Deployments list...');
    await page.click('a[href="/deployments"], text=Deployments');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Screenshot: Deployments list
    await page.screenshot({
      path: path.join(OUTPUT_DIR, '08-deployments-list.png'),
      fullPage: false
    });
    console.log('   Captured: 08-deployments-list.png');

    console.log('\nDone! Screenshots saved to:', OUTPUT_DIR);

  } catch (error) {
    console.error('Error:', error);
    // Capture error screenshot
    await page.screenshot({
      path: path.join(OUTPUT_DIR, 'error-screenshot.png'),
      fullPage: true
    });
  } finally {
    await browser.close();
  }
}

main();
