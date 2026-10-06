/**
 * Targets the browser leaves for come from a provider's discovery document: only absolute
 * http(s) URLs are followed (never javascript:, data: or relative URLs).
 */
export function isHttpUrl(url: string | null | undefined): url is string {
  if (!url) {
    return false;
  }
  try {
    const { protocol } = new URL(url);
    return protocol === 'https:' || protocol === 'http:';
  } catch {
    return false;
  }
}
