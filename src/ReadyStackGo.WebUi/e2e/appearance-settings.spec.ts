import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
// Images for the implementation PR of the new theme (Vorhaben #477).
const IMAGE_DIR = path.join(__dirname, '..', '..', '..', 'docs', 'plans', 'theme-und-logo', 'bilder');

/**
 * Settings → Appearance and the runtime-loaded theme packages.
 * Runs against a container with the admin account admin / Admin1234 (see wizard.spec.ts).
 */

async function login(page: Page) {
  await page.goto('/login');
  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'Admin1234');
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
}

async function resetThemeStorage(page: Page) {
  await page.evaluate(() => {
    localStorage.removeItem('colorTheme');
    localStorage.removeItem('colorThemeCss');
    localStorage.setItem('theme', 'light');
  });
}

const htmlTheme = (page: Page) => page.evaluate(() => document.documentElement.getAttribute('data-theme'));
const htmlIsDark = (page: Page) => page.evaluate(() => document.documentElement.classList.contains('dark'));

test.describe('Appearance settings', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await login(page);
    await resetThemeStorage(page);
  });

  test('settings index links to Appearance', async ({ page }) => {
    await page.goto('/settings');
    const card = page.getByRole('link', { name: /Appearance/ });
    await expect(card).toBeVisible();
    await expect(card).toContainText('Choose the color theme and light or dark mode');
    await card.click();
    await expect(page).toHaveURL(/\/settings\/appearance$/);
    await expect(page.getByRole('heading', { name: 'Appearance' })).toBeVisible();
  });

  test('offers the theme packages of the installation, turquoise by default', async ({ page }) => {
    await page.goto('/settings/appearance');
    const cards = page.getByRole('radio').filter({ has: page.locator('[data-theme]') });
    await expect(cards).toHaveCount(3);
    await expect(page.getByTestId('theme-card-turquoise')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('theme-card-pastel-green')).toHaveAttribute('aria-checked', 'false');
    await expect(page.getByTestId('theme-card-classic')).toHaveAttribute('aria-checked', 'false');
    expect(await htmlTheme(page)).toBe('turquoise');
  });

  test('selecting a theme applies it and survives a reload before the app loads', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('theme-card-pastel-green').click();
    expect(await htmlTheme(page)).toBe('pastel-green');
    await expect(page.getByTestId('theme-card-pastel-green')).toHaveAttribute('aria-checked', 'true');

    // The inline script in index.html sets the attribute before any module script runs.
    await page.route('**/assets/*.js', (route) => route.abort());
    await page.reload();
    expect(await htmlTheme(page)).toBe('pastel-green');
    await expect(page.locator('link[data-rsgo-theme="pastel-green"]')).toHaveCount(1);
    await page.unroute('**/assets/*.js');
  });

  test('the theme package actually changes the colors', async ({ page }) => {
    await page.goto('/settings/appearance');
    const sidebar = page.locator('aside');
    await expect(sidebar).toBeVisible();
    await expect(page.getByTestId('theme-card-classic')).toBeVisible();
    const navBg = () => sidebar.evaluate((el) => getComputedStyle(el).backgroundColor);
    const turquoise = await navBg();
    await page.getByTestId('theme-card-classic').click();
    // The sidebar animates its colors (transition-all), so wait for the final value.
    await expect.poll(navBg).toBe('rgb(255, 255, 255)'); // classic sidebar is white in light mode
    expect(turquoise).toBe('rgb(232, 251, 251)'); // turquoise/50
  });

  test('arrow keys move the selection', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('theme-card-turquoise').focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('theme-card-pastel-green')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('theme-card-pastel-green')).toBeFocused();
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await expect(page.getByTestId('theme-card-classic')).toHaveAttribute('aria-checked', 'true');
    expect(await htmlTheme(page)).toBe('classic');
  });

  test('mode switch and header toggle stay in sync', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('mode-dark').click();
    expect(await htmlIsDark(page)).toBe(true);
    await expect(page.getByTestId('mode-dark')).toHaveAttribute('aria-checked', 'true');

    await page.getByRole('button', { name: 'Toggle theme' }).click();
    expect(await htmlIsDark(page)).toBe(false);
    await expect(page.getByTestId('mode-light')).toHaveAttribute('aria-checked', 'true');
  });

  test.describe('stored values', () => {
    for (const stored of ['blue', 'Classic', '../etc', '']) {
      test(`falls back to turquoise for stored value "${stored}"`, async ({ page }) => {
        await page.evaluate((v) => localStorage.setItem('colorTheme', v), stored);
        await page.goto('/settings/appearance');
        await expect(page.getByTestId('theme-card-turquoise')).toHaveAttribute('aria-checked', 'true');
        expect(await htmlTheme(page)).toBe('turquoise');
        expect(await page.evaluate(() => localStorage.getItem('colorTheme'))).toBe('turquoise');
      });
    }
  });

  test('captures images of the design states', async ({ page }) => {
    const shots: [string, string, 'light' | 'dark'][] = [
      ['/settings/appearance', 'turquoise', 'light'],
      ['/settings/appearance', 'turquoise', 'dark'],
      ['/settings/appearance', 'pastel-green', 'light'],
      ['/settings/appearance', 'classic', 'dark'],
      ['/deployments', 'turquoise', 'light'],
      ['/deployments', 'turquoise', 'dark'],
      ['/deployments', 'pastel-green', 'light'],
      ['/deployments', 'pastel-green', 'dark'],
      ['/settings', 'turquoise', 'light'],
      ['/', 'turquoise', 'light'],
      ['/', 'classic', 'light'],
    ];
    for (const [route, theme, mode] of shots) {
      await page.evaluate(
        ([t, m]) => {
          localStorage.setItem('colorTheme', t);
          localStorage.setItem('colorThemeCss', `/api/themes/${t}/theme.css`);
          localStorage.setItem('theme', m);
        },
        [theme, mode],
      );
      await page.goto(route);
      await page.waitForLoadState('networkidle');
      await page.evaluate(() => document.fonts.ready);
      const name = route === '/' ? 'dashboard' : route.replace(/^\//, '').replace(/\//g, '-');
      await page.screenshot({ path: path.join(IMAGE_DIR, `app-${name}-${theme}-${mode}.png`) });
    }

    // Collapsed sidebar (toggle button in the header).
    for (const [theme, mode] of [['turquoise', 'light'], ['turquoise', 'dark']] as const) {
      await page.evaluate(
        ([t, m]) => {
          localStorage.setItem('colorTheme', t);
          localStorage.setItem('colorThemeCss', `/api/themes/${t}/theme.css`);
          localStorage.setItem('theme', m);
        },
        [theme, mode],
      );
      await page.goto('/deployments');
      await page.waitForLoadState('networkidle');
      await page.locator('header button').first().click();
      await page.mouse.move(1200, 600);
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(IMAGE_DIR, `app-deployments-collapsed-${theme}-${mode}.png`) });
    }
  });
});
