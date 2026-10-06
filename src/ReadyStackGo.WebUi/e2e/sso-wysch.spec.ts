import { test, expect, type Page } from '@playwright/test';
import { execFileSync } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * Single sign-on against the container with the test identity provider (plan
 * docs/plans/identity-provider-vorlagen.md, section 7 "Browsertest"). Runs only with
 * playwright.sso.config.ts on a fresh installation (scripts/sso-e2e.sh): the template "wysch"
 * points at http://test-idp:9090/, "Company SSO" comes from the templates directory.
 *
 * The steps build on each other (fresh wizard → WYSCH admin → providers → revocation →
 * emergency access), so they run as one serial suite.
 */

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const DOCS_IMAGES = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');
const PR_IMAGES = path.join(__dirname, '..', '..', '..', 'docs', 'plans', 'identity-provider-vorlagen', 'bilder');
const IDP = 'http://localhost:9090';
const CONTAINER = process.env.RSGO_CONTAINER || 'rsgo-sso-e2e';

fs.mkdirSync(PR_IMAGES, { recursive: true });

async function shot(page: Page, name: string, options: { docs?: string; dark?: boolean } = {}) {
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: path.join(PR_IMAGES, `${name}.png`), fullPage: true });
  if (options.docs) {
    await page.screenshot({ path: path.join(DOCS_IMAGES, options.docs), fullPage: false });
  }
  if (options.dark) {
    // Colour transitions would otherwise be caught halfway in the screenshot.
    await page.addStyleTag({ content: '*, *::before, *::after { transition: none !important; }' });
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.evaluate(() => {
      document.documentElement.dataset.mode = 'dark';
      document.documentElement.classList.add('dark');
    });
    await page.screenshot({ path: path.join(PR_IMAGES, `${name}-dunkel.png`), fullPage: true });
    await page.evaluate(() => {
      document.documentElement.dataset.mode = 'light';
      document.documentElement.classList.remove('dark');
    });
    await page.emulateMedia({ colorScheme: 'light' });
  }
}

/** Calls the ReadyStackGo API with the token of the signed-in user. */
async function api<T>(page: Page, method: string, url: string, body?: unknown): Promise<T> {
  return page.evaluate(
    async ({ method, url, body }) => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch(url, {
        method,
        headers: {
          Authorization: `Bearer ${token}`,
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
      const text = await response.text();
      return (text ? JSON.parse(text) : undefined) as T;
    },
    { method, url, body },
  );
}

async function signOut(page: Page) {
  // A fresh page is still on about:blank, which has no localStorage of the app.
  if (!page.url().startsWith('http')) await page.goto('/login');
  await page.evaluate(() => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
  });
  await page.goto('/login');
}

async function signInWithPassword(page: Page, username: string, password: string) {
  await page.goto('/login');
  await page.getByPlaceholder('admin@example.com').fill(username);
  await page.getByPlaceholder('Enter your password').fill(password);
  await page.getByRole('button', { name: /^Sign in$/ }).click();
  await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 15000 });
}

/** Picks a test user on the sign-in page of the test identity provider. */
async function pickTestUser(page: Page, name: string) {
  await expect(page.getByRole('heading', { name: 'Sign in to the test identity provider' })).toBeVisible();
  await page.getByRole('button', { name: `Sign in as ${name}` }).click();
}

test.describe.configure({ mode: 'serial' });

test.describe('Single sign-on with WYSCH (test identity provider)', () => {
  test('setup wizard: WYSCH account becomes the first administrator', async ({ page }) => {
    await page.goto('/wizard');
    await expect(page.getByRole('heading', { name: 'How do you want to sign in?' })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Continue$/ })).toBeDisabled();
    await shot(page, 'wizard-anmeldeart-leer');

    await page.getByTestId('sign-in-option-wysch').click();
    await shot(page, 'wizard-anmeldeart-wysch', { docs: 'sso-01-wizard-method.png', dark: true });
    await page.getByRole('button', { name: /^Continue$/ }).click();

    // Address of this installation: http on a non-loopback host is refused.
    const address = page.getByTestId('sso-base-url');
    await expect(address).toHaveValue('http://localhost:8080');
    await shot(page, 'wizard-wysch-adresse');
    await address.fill('http://server:8080');
    await expect(page.getByTestId('sso-https-required')).toBeVisible();
    await expect(page.getByTestId('sso-connect')).toBeDisabled();
    await shot(page, 'wizard-wysch-ohne-https');
    await address.fill('http://localhost:8080');

    // Pairing at the provider: cancel first, then "Try again" and connect.
    await page.getByTestId('sso-connect').click();
    await expect(page.getByRole('heading', { name: 'Connect an application' })).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByTestId('sso-error')).toContainText('Connection cancelled');
    await shot(page, 'wizard-wysch-fehler-abgebrochen');

    await page.getByRole('button', { name: 'Try again' }).click();
    await expect(page.getByRole('heading', { name: 'Connect an application' })).toBeVisible();
    await page.getByRole('button', { name: 'Connect' }).click();

    // Back in the wizard, ReadyStackGo redeems the code and sends the browser to the sign-in.
    await pickTestUser(page, 'Alex Verified');
    await expect(page.getByTestId('sso-signed-in')).toContainText('Signed in as alex');
    await expect(page.getByTestId('sso-signed-in')).toContainText('username alex');
    await shot(page, 'wizard-wysch-angemeldet');

    await page.getByRole('button', { name: /^Continue$/ }).click();
    await expect(page.getByRole('heading', { name: /Configure email/ })).toBeVisible();
    await page.getByRole('button', { name: 'Skip for now' }).click();
    await page.waitForURL(/\/onboarding/, { timeout: 15000 });

    // Finish onboarding through the API so the settings are reachable.
    await api(page, 'POST', '/api/organizations', { id: 'sso-org', name: 'SSO Org' });
    await api(page, 'POST', '/api/onboarding/dismiss', {});
  });

  test('provider list warns while no system administrator has a local password', async ({ page }) => {
    await page.goto('/login');
    await page.getByTestId('sign-in-with-wysch').click();
    await pickTestUser(page, 'Alex Verified');
    await page.waitForURL((url) => !url.pathname.startsWith('/login') && !url.pathname.startsWith('/oidc-callback'));

    await page.goto('/settings/oidc');
    await expect(page.getByTestId('no-password-warning')).toContainText('The only system administrator has no local password');
    const row = page.getByTestId('provider-wysch');
    await expect(row).toContainText('Enabled');
    await shot(page, 'sso-liste', { docs: 'sso-02-provider-list.png', dark: true });

    // The provider page protects against lockout.
    await row.getByRole('link', { name: 'WYSCH' }).click();
    await expect(page.getByTestId('lockout-warning')).toBeVisible();
    await expect(page.getByTestId('remove-provider')).toBeDisabled();
    await expect(page.getByTestId('enable-provider')).toBeDisabled();
    await shot(page, 'sso-provider-wysch');
  });

  test('add provider from the templates directory: checks, test sign-in, save', async ({ page }) => {
    await signInWithSso(page);
    await page.goto('/settings/oidc/add');
    await expect(page.getByTestId('template-company-sso')).toBeVisible();
    await page.getByTestId('template-company-sso').click();
    await shot(page, 'sso-lauf-vorlage');
    await page.getByTestId('step-primary').click();

    // Provider address: wrong path first.
    await page.getByTestId('authority').fill('http://test-idp:9090/realms/main');
    await page.getByTestId('step-primary').click();
    await expect(page.getByTestId('discovery-result')).toHaveAttribute('data-result', 'failed');
    await shot(page, 'sso-lauf-anbieter-adresse-fehler');
    await page.getByTestId('authority').fill('http://test-idp:9090/');
    await page.getByTestId('step-primary').click();

    // This installation: name "company" and the redirect URI.
    await expect(page.getByTestId('provider-name')).toHaveValue('company');
    await expect(page.getByTestId('redirect-uri')).toHaveValue('http://localhost:8080/api/auth/oidc/company/callback');
    await shot(page, 'sso-lauf-installation');
    await page.getByTestId('step-primary').click();

    // Register with a wrong secret: the PAR check rejects it with its own message.
    await page.getByTestId('client-id').fill('rsgo-manual');
    await page.getByTestId('client-secret').fill('wrong-secret');
    await shot(page, 'sso-lauf-manuell');
    await page.getByTestId('step-primary').click();
    await expect(page.getByTestId('checks-failed')).toBeVisible();
    await expect(page.getByTestId('check-client')).toHaveAttribute('data-result', 'failed');
    await shot(page, 'sso-test-pruefungen-fehler');

    await page.getByRole('button', { name: 'Back' }).click();
    await page.getByTestId('client-secret').fill('e2e-manual-secret');
    await page.getByTestId('step-primary').click();
    await expect(page.getByTestId('check-client')).toHaveAttribute('data-result', 'passed');
    await shot(page, 'sso-test-pruefungen-ok');

    // Test sign-in with an unverified email: passes with a warning.
    await page.getByTestId('run-test-sign-in').click();
    await pickTestUser(page, 'Uma Unverified');
    await expect(page.getByTestId('test-result')).toContainText('The email address is not confirmed');
    await shot(page, 'sso-test-claims-unvollstaendig');

    await page.getByTestId('run-test-sign-in').click();
    await pickTestUser(page, 'Alex Verified');
    await expect(page.getByTestId('test-result')).toContainText('These details are enough to sign in');
    await expect(page.getByTestId('claims-table')).toContainText('preferred_username');
    await shot(page, 'sso-test-claims-ok', { docs: 'sso-03-test-sign-in.png' });

    await page.getByTestId('step-primary').click();
    await expect(page.getByTestId('enable-provider')).toBeEnabled();
    await shot(page, 'sso-lauf-speichern');
    await page.getByTestId('step-primary').click();
    await page.waitForURL(/\/settings\/oidc\?saved=company/);
    await expect(page.getByTestId('provider-company')).toContainText('Enabled');
  });

  test('sign-in page: provider buttons; unverified email is not matched', async ({ page }) => {
    await signOut(page);
    await expect(page.getByTestId('sign-in-with-wysch')).toBeVisible();
    await expect(page.getByTestId('sign-in-with-company')).toBeVisible();
    await shot(page, 'login-provider', { dark: true });

    await page.getByTestId('sign-in-with-company').click();
    await pickTestUser(page, 'Uma Unverified');
    await page.waitForURL(/\/login\?error=/);
    await expect(page.getByRole('alert')).toContainText('not confirmed');
  });

  test('profile: Unlink locked without password, then set a local password', async ({ page }) => {
    await signInWithSso(page);
    await page.goto('/profile');
    await expect(page.getByTestId('no-password-warning')).toBeVisible();
    await expect(page.getByTestId('password-not-set')).toBeVisible();
    await expect(page.getByTestId('unlink-wysch')).toBeDisabled();
    await shot(page, 'profil-ohne-passwort', { dark: true });

    await page.getByTestId('local-password').fill('Local1234');
    await page.getByTestId('local-password-confirm').fill('Local1234');
    await page.getByRole('button', { name: 'Set password' }).click();
    await expect(page.getByTestId('set-local-password')).toBeHidden();
    await expect(page.getByTestId('unlink-wysch')).toBeEnabled();

    await signOut(page);
    await signInWithPassword(page, 'alex', 'Local1234');
  });

  test('revocation: "Reconnect needed", then Reconnect with a test sign-in', async ({ page, request }) => {
    await signInWithPassword(page, 'alex', 'Local1234');
    const settings = await api<{ providers: { name: string; clientId: string }[] }>(page, 'GET', '/api/settings/oidc');
    const clientId = settings.providers.find((p) => p.name === 'wysch')!.clientId;
    expect((await request.post(`${IDP}/test/clients/${encodeURIComponent(clientId)}/revoke`)).ok()).toBeTruthy();

    await signOut(page);
    await page.getByTestId('sign-in-with-wysch').click();
    await page.waitForURL(/\/login\?error=oidc_provider_rejected/);
    await expect(page.getByRole('alert')).toContainText('reconnect');

    await signInWithPassword(page, 'alex', 'Local1234');
    await page.goto('/settings/oidc');
    await expect(page.getByTestId('provider-wysch')).toContainText('Reconnect needed');
    await shot(page, 'sso-liste-reconnect');

    await page.getByTestId('provider-wysch').getByRole('button', { name: 'Reconnect' }).click();
    await page.getByTestId('connect').click();
    await expect(page.getByRole('heading', { name: 'Connect an application' })).toBeVisible();
    await page.getByRole('button', { name: 'Connect', exact: true }).click();
    await expect(page.getByTestId('connected')).toBeVisible();
    await shot(page, 'sso-reconnect-verbunden');
    await page.getByTestId('step-primary').click();
    await expect(page.getByTestId('check-client')).toHaveAttribute('data-result', 'passed');
    await page.getByTestId('run-test-sign-in').click();
    await pickTestUser(page, 'Alex Verified');
    await expect(page.getByTestId('test-result')).toContainText('These details are enough to sign in');

    await page.goto('/settings/oidc');
    await expect(page.getByTestId('provider-wysch')).not.toContainText('Reconnect needed');
  });

  test('emergency access: rsgo admin set-password inside the container', async ({ page }) => {
    const output = execFileSync('docker', ['exec', '-i', CONTAINER, 'rsgo', 'admin', 'set-password', 'alex'], {
      input: 'Emergency123\n',
    }).toString();
    expect(output).toContain("Emergency access: local password set for user 'alex'");

    await signInWithPassword(page, 'alex', 'Emergency123');
    await expect(page).not.toHaveURL(/\/login/);
  });
});

/** Signs in with WYSCH as Alex Verified. */
async function signInWithSso(page: Page) {
  await page.goto('/login');
  await page.getByTestId('sign-in-with-wysch').click();
  await pickTestUser(page, 'Alex Verified');
  await page.waitForURL((url) => !url.pathname.startsWith('/login') && !url.pathname.startsWith('/oidc-callback'));
}
