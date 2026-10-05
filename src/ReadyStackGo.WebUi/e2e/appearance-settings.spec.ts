import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
// Images of the compact Settings → Appearance (design extension of Vorhaben #477, PR #488).
const IMAGE_DIR = path.join(__dirname, '..', '..', '..', 'docs', 'plans', 'theme-und-logo', 'bilder', 'appearance-kompakt');

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
const htmlMode = (page: Page) => page.evaluate(() => document.documentElement.getAttribute('data-mode'));
const storedMode = (page: Page) => page.evaluate(() => localStorage.getItem('theme'));

const ONLY_CLASSIC = {
  default: 'classic',
  themes: [{ id: 'classic', name: 'Classic', description: null, cssUrl: '/api/themes/classic/theme.css' }],
};

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
    const orbs = page.getByRole('radio').filter({ has: page.locator('[data-theme]') });
    await expect(orbs).toHaveCount(3);
    await expect(page.getByTestId('theme-orb-turquoise')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('theme-orb-pastel-green')).toHaveAttribute('aria-checked', 'false');
    await expect(page.getByTestId('theme-orb-classic')).toHaveAttribute('aria-checked', 'false');
    expect(await htmlTheme(page)).toBe('turquoise');
  });

  test('selecting a theme applies it and survives a reload before the app loads', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('theme-orb-pastel-green').click();
    expect(await htmlTheme(page)).toBe('pastel-green');
    await expect(page.getByTestId('theme-orb-pastel-green')).toHaveAttribute('aria-checked', 'true');

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
    await expect(page.getByTestId('theme-orb-classic')).toBeVisible();
    const navBg = () => sidebar.evaluate((el) => getComputedStyle(el).backgroundColor);
    const turquoise = await navBg();
    await page.getByTestId('theme-orb-classic').click();
    // The sidebar animates its colors (transition-all), so wait for the final value.
    await expect.poll(navBg).toBe('rgb(255, 255, 255)'); // classic sidebar is white in light mode
    expect(turquoise).toBe('rgb(232, 251, 251)'); // turquoise/50
  });

  test('arrow keys move the selection', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('theme-orb-turquoise').focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('theme-orb-pastel-green')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('theme-orb-pastel-green')).toBeFocused();
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await expect(page.getByTestId('theme-orb-classic')).toHaveAttribute('aria-checked', 'true');
    expect(await htmlTheme(page)).toBe('classic');
  });

  test('each orb shows its theme in light and dark, whatever the page mode', async ({ page }) => {
    await page.goto('/settings/appearance');
    const halves = () =>
      page.getByTestId('theme-orb-turquoise').evaluate((el) => {
        const bg = (mode: string) =>
          getComputedStyle(el.querySelector(`[data-mode="${mode}"]`) as Element).backgroundColor;
        return { light: bg('light'), dark: bg('dark') };
      });
    const expected = { light: 'rgb(232, 251, 251)', dark: 'rgb(7, 26, 33)' }; // turquoise nav-bg per mode
    expect(await halves()).toEqual(expected);
    await page.getByTestId('mode-dark').click();
    expect(await htmlIsDark(page)).toBe(true);
    expect(await halves()).toEqual(expected);
  });

  test('hides the theme section when only one theme is offered', async ({ page }) => {
    await page.route('**/api/themes', (route) => route.fulfill({ json: ONLY_CLASSIC }));
    await page.goto('/settings/appearance');
    await expect(page.getByRole('heading', { name: 'Mode' })).toBeVisible();
    await expect.poll(() => htmlTheme(page)).toBe('classic');
    await expect(page.getByRole('heading', { name: 'Theme' })).toHaveCount(0);
    await expect(page.locator('[data-testid^="theme-orb-"]')).toHaveCount(0);
  });

  test('mode switch and header toggle stay in sync', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('mode-dark').click();
    expect(await htmlIsDark(page)).toBe(true);
    expect(await htmlMode(page)).toBe('dark');
    await expect(page.getByTestId('mode-dark')).toHaveAttribute('aria-checked', 'true');

    await page.getByRole('button', { name: 'Toggle theme' }).click();
    expect(await htmlIsDark(page)).toBe(false);
    expect(await htmlMode(page)).toBe('light');
    await expect(page.getByTestId('mode-light')).toHaveAttribute('aria-checked', 'true');
  });

  test('arrow keys move the mode selection', async ({ page }) => {
    await page.goto('/settings/appearance');
    await page.getByTestId('mode-light').focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('mode-dark')).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByTestId('mode-dark')).toBeFocused();
    await page.keyboard.press('End');
    await expect(page.getByTestId('mode-system')).toHaveAttribute('aria-checked', 'true');
    await page.keyboard.press('ArrowRight');
    await expect(page.getByTestId('mode-light')).toHaveAttribute('aria-checked', 'true');
  });

  test.describe('system mode', () => {
    test('follows the operating system and changes with it', async ({ page }) => {
      await page.emulateMedia({ colorScheme: 'dark' });
      await page.goto('/settings/appearance');
      await page.getByTestId('mode-system').click();
      expect(await storedMode(page)).toBe('system');
      expect(await htmlIsDark(page)).toBe(true);

      await page.emulateMedia({ colorScheme: 'light' });
      await expect.poll(() => htmlMode(page)).toBe('light');
      expect(await htmlIsDark(page)).toBe(false);
      await expect(page.getByTestId('mode-system')).toHaveAttribute('aria-checked', 'true');

      await page.emulateMedia({ colorScheme: 'dark' });
      await expect.poll(() => htmlMode(page)).toBe('dark');
    });

    test('explicit light ignores the operating system', async ({ page }) => {
      await page.emulateMedia({ colorScheme: 'dark' });
      await page.goto('/settings/appearance');
      await page.getByTestId('mode-light').click();
      expect(await htmlMode(page)).toBe('light');
      await page.emulateMedia({ colorScheme: 'light' });
      await page.emulateMedia({ colorScheme: 'dark' });
      expect(await htmlMode(page)).toBe('light');
    });

    test('the header button leaves system for the opposite of what is shown', async ({ page }) => {
      await page.emulateMedia({ colorScheme: 'dark' });
      await page.goto('/settings/appearance');
      await page.getByTestId('mode-system').click();
      await page.getByRole('button', { name: 'Toggle theme' }).click();
      expect(await htmlMode(page)).toBe('light');
      expect(await storedMode(page)).toBe('light');
      await expect(page.getByTestId('mode-light')).toHaveAttribute('aria-checked', 'true');
    });

    test('applies the system mode before the app loads', async ({ page }) => {
      await page.emulateMedia({ colorScheme: 'dark' });
      await page.evaluate(() => localStorage.setItem('theme', 'system'));
      await page.route('**/assets/*.js', (route) => route.abort());
      await page.goto('/settings/appearance');
      expect(await htmlMode(page)).toBe('dark');
      expect(await htmlIsDark(page)).toBe(true);
      await page.unroute('**/assets/*.js');
    });

    for (const stored of ['auto', 'Dark', '']) {
      test(`treats stored mode "${stored}" as system`, async ({ page }) => {
        await page.emulateMedia({ colorScheme: 'dark' });
        await page.evaluate((v) => localStorage.setItem('theme', v), stored);
        await page.goto('/settings/appearance');
        await expect(page.getByTestId('mode-system')).toHaveAttribute('aria-checked', 'true');
        expect(await htmlMode(page)).toBe('dark');
        expect(await storedMode(page)).toBe('system');
      });
    }
  });

  test.describe('stored values', () => {
    for (const stored of ['blue', 'Classic', '../etc', '']) {
      test(`falls back to turquoise for stored value "${stored}"`, async ({ page }) => {
        await page.evaluate((v) => localStorage.setItem('colorTheme', v), stored);
        await page.goto('/settings/appearance');
        await expect(page.getByTestId('theme-orb-turquoise')).toHaveAttribute('aria-checked', 'true');
        expect(await htmlTheme(page)).toBe('turquoise');
        expect(await page.evaluate(() => localStorage.getItem('colorTheme'))).toBe('turquoise');
      });
    }
  });

  test('captures images of the design states', async ({ page }) => {
    const shots: [string, string, 'light' | 'dark' | 'system'][] = [
      ['app-settings-appearance-turquoise-light', 'turquoise', 'light'],
      ['app-settings-appearance-turquoise-dark', 'turquoise', 'dark'],
      ['app-settings-appearance-pastel-green-system', 'pastel-green', 'system'],
    ];
    await page.emulateMedia({ colorScheme: 'light' });
    for (const [name, theme, mode] of shots) {
      await page.evaluate(
        ([t, m]) => {
          localStorage.setItem('colorTheme', t);
          localStorage.setItem('colorThemeCss', `/api/themes/${t}/theme.css`);
          localStorage.setItem('theme', m);
        },
        [theme, mode],
      );
      await page.goto('/settings/appearance');
      await page.waitForLoadState('networkidle');
      await page.evaluate(() => document.fonts.ready);
      await page.mouse.move(1400, 880);
      await page.screenshot({ path: path.join(IMAGE_DIR, `${name}.png`) });
    }

    // Only one theme offered (Classic), dark.
    await page.route('**/api/themes', (route) => route.fulfill({ json: ONLY_CLASSIC }));
    await page.evaluate(() => {
      localStorage.removeItem('colorTheme');
      localStorage.removeItem('colorThemeCss');
      localStorage.setItem('theme', 'dark');
    });
    await page.goto('/settings/appearance');
    await page.waitForLoadState('networkidle');
    await expect.poll(() => htmlTheme(page)).toBe('classic');
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(IMAGE_DIR, 'app-settings-appearance-one-theme-classic-dark.png') });
  });
});
