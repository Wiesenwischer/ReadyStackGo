// Rules for the address of this installation, the same as on the server (BaseUrlRules).

/** Suggestion for the address of this installation: the address in the browser (E8). */
export function suggestBaseUrl(origin: string): string {
  return origin.replace(/\/+$/, "");
}

/** Normalizes a base URL like the server: absolute http(s), no query, fragment or user info, no trailing slash. */
export function normalizeBaseUrl(value: string): string | null {
  let url: URL;
  try {
    url = new URL(value.trim());
  } catch {
    return null;
  }
  if (url.protocol !== "http:" && url.protocol !== "https:") return null;
  if (url.search || url.hash || url.username || url.password || !url.hostname) return null;
  return `${url.protocol}//${url.host}${url.pathname}`.replace(/\/+$/, "");
}

/** HTTPS rule of templates that require it: https, or http on loopback (localhost, 127.0.0.0/8, [::1]). */
export function satisfiesHttpsRequirement(baseUrl: string): boolean {
  let url: URL;
  try {
    url = new URL(baseUrl);
  } catch {
    return false;
  }
  if (url.protocol === "https:") return true;
  if (url.protocol !== "http:") return false;
  const host = url.hostname.toLowerCase();
  return host === "localhost" || /^127(\.\d{1,3}){3}$/.test(host) || host === "[::1]" || host === "::1";
}
