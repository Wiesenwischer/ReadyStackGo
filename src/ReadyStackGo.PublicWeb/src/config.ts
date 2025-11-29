/**
 * Central configuration for ReadyStackGo PublicWeb
 * Change these values when the site moves to a new domain
 *
 * NOTE: Markdown files in src/content/docs/ also contain the install URL.
 * When changing the domain, search for "readystackgo.pages.dev" and update those files too.
 */

export const config = {
	/**
	 * Base URL for downloads and install scripts
	 * Set to empty string for dynamic resolution (uses current origin)
	 * Set to a specific URL to override (e.g., 'https://readystackgo.pages.dev')
	 */
	baseUrl: 'https://readystackgo.pages.dev',

	/**
	 * Install script path (relative to baseUrl)
	 */
	installScriptPath: '/install.sh',
} as const;

/**
 * Get the full install script URL
 * Uses configured baseUrl or falls back to current origin
 */
export function getInstallScriptUrl(currentOrigin?: string): string {
	const base = config.baseUrl || currentOrigin || 'https://readystackgo.pages.dev';
	return `${base}${config.installScriptPath}`;
}

/**
 * Get the curl command for installation
 */
export function getInstallCommand(currentOrigin?: string): string {
	return `curl -fsSL ${getInstallScriptUrl(currentOrigin)} | sudo bash`;
}

/**
 * Get the curl command with custom port
 */
export function getInstallCommandWithPort(port: string | number, currentOrigin?: string): string {
	return `curl -fsSL ${getInstallScriptUrl(currentOrigin)} | sudo bash -s ${port}`;
}
