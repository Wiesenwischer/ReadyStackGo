import { test, expect } from '@playwright/test';

/**
 * E2E Tests for Static File Serving
 * These tests verify that static files (JS, CSS) are served with correct MIME types
 * This is critical for the SPA to load correctly in the Docker container
 */

test.describe('Static File Serving', () => {
  test('should serve JavaScript files with correct MIME type', async ({ request }) => {
    // Get the index page to find JS file references
    const indexResponse = await request.get('/');
    expect(indexResponse.ok()).toBe(true);

    const html = await indexResponse.text();

    // Find JS file reference in the HTML
    const jsMatch = html.match(/src="(\/assets\/index-[^"]+\.js)"/);
    expect(jsMatch).toBeTruthy();

    const jsPath = jsMatch![1];

    // Fetch the JS file directly
    const jsResponse = await request.get(jsPath);
    expect(jsResponse.ok()).toBe(true);

    // Check MIME type
    const contentType = jsResponse.headers()['content-type'];
    expect(contentType).toMatch(/javascript|application\/javascript|text\/javascript/);

    // Verify it's actual JavaScript, not HTML (SPA fallback)
    const content = await jsResponse.text();
    expect(content).not.toContain('<!DOCTYPE html>');
    expect(content).not.toContain('<html');
  });

  test('should serve CSS files with correct MIME type', async ({ request }) => {
    // Get the index page to find CSS file references
    const indexResponse = await request.get('/');
    expect(indexResponse.ok()).toBe(true);

    const html = await indexResponse.text();

    // Find CSS file reference in the HTML
    const cssMatch = html.match(/href="(\/assets\/index-[^"]+\.css)"/);
    expect(cssMatch).toBeTruthy();

    const cssPath = cssMatch![1];

    // Fetch the CSS file directly
    const cssResponse = await request.get(cssPath);
    expect(cssResponse.ok()).toBe(true);

    // Check MIME type
    const contentType = cssResponse.headers()['content-type'];
    expect(contentType).toMatch(/text\/css/);

    // Verify it's actual CSS, not HTML (SPA fallback)
    const content = await cssResponse.text();
    expect(content).not.toContain('<!DOCTYPE html>');
    expect(content).not.toContain('<html');
  });

  test('should serve index.html for root route', async ({ request }) => {
    const response = await request.get('/');
    expect(response.ok()).toBe(true);

    const contentType = response.headers()['content-type'];
    expect(contentType).toContain('text/html');

    const content = await response.text();
    expect(content).toContain('<!DOCTYPE html>');
    expect(content).toContain('<div id="root">');
  });

  test('should serve SPA fallback for unknown frontend routes', async ({ request }) => {
    const response = await request.get('/some/random/route');
    expect(response.ok()).toBe(true);

    const contentType = response.headers()['content-type'];
    expect(contentType).toContain('text/html');

    const content = await response.text();
    expect(content).toContain('<!DOCTYPE html>');
    expect(content).toContain('<div id="root">');
  });

  test('should NOT serve SPA fallback for API routes', async ({ request }) => {
    const response = await request.get('/api/nonexistent');

    // API routes should return an error, not the SPA
    expect(response.ok()).toBe(false);

    const contentType = response.headers()['content-type'];
    // Should NOT be HTML
    expect(contentType).not.toContain('text/html');
  });

  test('should serve favicon', async ({ request }) => {
    // Try different favicon paths that might exist
    const paths = ['/favicon.png', '/favicon.ico', '/vite.svg'];

    let foundFavicon = false;
    for (const path of paths) {
      const response = await request.get(path);
      if (response.ok()) {
        foundFavicon = true;
        const contentType = response.headers()['content-type'];
        // Should be an image type, not HTML
        expect(contentType).not.toContain('text/html');
        break;
      }
    }

    expect(foundFavicon).toBe(true);
  });

  test('SPA should load and render correctly', async ({ page }) => {
    // This test verifies the full chain:
    // 1. HTML loads
    // 2. JS loads with correct MIME type
    // 3. React app renders

    // Check for no console errors related to MIME types
    const consoleErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });

    // Navigate to the app
    await page.goto('/');

    // Wait for page to load
    await page.waitForLoadState('networkidle');

    // Check for MIME type errors
    const mimeErrors = consoleErrors.filter(
      err =>
        err.includes('MIME type') ||
        err.includes('module script') ||
        err.includes('text/html')
    );

    expect(mimeErrors).toHaveLength(0);

    // Verify React app rendered (either login page or app content)
    const hasContent = await page
      .locator('#root')
      .evaluate(el => el.children.length > 0);
    expect(hasContent).toBe(true);
  });
});
