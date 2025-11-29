const { chromium } = require('playwright');
const path = require('path');

const BASE_URL = 'http://localhost:8080';
const SCREENSHOTS_DIR = path.join(__dirname, 'public', 'images', 'screenshots');

async function takeScreenshots() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1400, height: 900 },
    colorScheme: 'light'
  });
  const page = await context.newPage();

  try {
    // Go to app - should show wizard on fresh install
    console.log('Navigating to app...');
    await page.goto(BASE_URL, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);

    // Screenshot 1: Wizard Admin Step
    console.log('Taking screenshot: wizard-admin.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'wizard-admin.png'),
      fullPage: false
    });

    // Fill admin form and proceed
    const adminUsername = await page.$('input[placeholder*="admin"], input[name="username"]');
    if (adminUsername) {
      console.log('Filling wizard admin form...');
      await page.fill('input[placeholder*="admin"], input[name="username"]', 'admin');
      await page.fill('input[type="password"]', 'Admin123!');
      // Find confirm password if exists
      const confirmPassword = await page.$$('input[type="password"]');
      if (confirmPassword.length > 1) {
        await confirmPassword[1].fill('Admin123!');
      }
      await page.waitForTimeout(500);

      // Click next/continue button
      const nextButton = await page.$('button:has-text("Next"), button:has-text("Weiter"), button:has-text("Continue")');
      if (nextButton) {
        await nextButton.click();
        await page.waitForTimeout(1500);
      }
    }

    // Screenshot 2: Wizard Organization Step
    console.log('Taking screenshot: wizard-org.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'wizard-org.png'),
      fullPage: false
    });

    // Fill organization form
    const orgId = await page.$('input[placeholder*="org"], input[name*="org"]');
    if (orgId) {
      console.log('Filling wizard organization form...');
      await page.fill('input[placeholder*="org"], input[name*="org"]', 'my-company');
      const orgName = await page.$('input[placeholder*="name"], input[name*="name"]');
      if (orgName) {
        await orgName.fill('My Company Inc.');
      }
      await page.waitForTimeout(500);

      const nextButton = await page.$('button:has-text("Next"), button:has-text("Weiter"), button:has-text("Continue")');
      if (nextButton) {
        await nextButton.click();
        await page.waitForTimeout(1500);
      }
    }

    // Screenshot 3: Wizard Environment Step
    console.log('Taking screenshot: wizard-env.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'wizard-env.png'),
      fullPage: false
    });

    // Skip or complete environment step
    const skipButton = await page.$('button:has-text("Skip"), button:has-text("Überspringen")');
    const nextButton = await page.$('button:has-text("Next"), button:has-text("Weiter"), button:has-text("Continue"), button:has-text("Complete"), button:has-text("Finish")');
    if (skipButton) {
      await skipButton.click();
      await page.waitForTimeout(1500);
    } else if (nextButton) {
      await nextButton.click();
      await page.waitForTimeout(1500);
    }

    // Screenshot 4: Wizard Complete Step
    console.log('Taking screenshot: wizard-complete.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'wizard-complete.png'),
      fullPage: false
    });

    // Complete setup
    const completeButton = await page.$('button:has-text("Complete"), button:has-text("Finish"), button:has-text("Abschließen"), button:has-text("Start")');
    if (completeButton) {
      await completeButton.click();
      await page.waitForTimeout(2000);
    }

    // Now we should be at login or dashboard
    // If at login, log in
    const loginForm = await page.$('form');
    const loginButton = await page.$('button:has-text("Sign in"), button:has-text("Login"), button:has-text("Anmelden")');
    if (loginButton) {
      console.log('Taking screenshot: login.png');
      await page.screenshot({
        path: path.join(SCREENSHOTS_DIR, 'login.png'),
        fullPage: false
      });

      console.log('Logging in...');
      await page.fill('input[type="text"], input[name="username"]', 'admin');
      await page.fill('input[type="password"]', 'Admin123!');
      await loginButton.click();
      await page.waitForTimeout(3000);
    }

    // Screenshot: Dashboard
    console.log('Taking screenshot: dashboard.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'dashboard.png'),
      fullPage: false
    });

    // Navigate to Stacks
    console.log('Navigating to Stacks...');
    await page.goto(BASE_URL + '/stacks', { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    console.log('Taking screenshot: stacks.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'stacks.png'),
      fullPage: false
    });

    // Navigate to Environments
    console.log('Navigating to Environments...');
    await page.goto(BASE_URL + '/environments', { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    console.log('Taking screenshot: environments.png');
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'environments.png'),
      fullPage: false
    });

    console.log('Screenshots completed!');
  } catch (error) {
    console.error('Error taking screenshots:', error);
    // Take a debug screenshot
    await page.screenshot({
      path: path.join(SCREENSHOTS_DIR, 'debug-error.png'),
      fullPage: true
    });
  } finally {
    await browser.close();
  }
}

takeScreenshots();
