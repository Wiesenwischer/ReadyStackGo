import { describe, expect, it } from 'vitest';
import { isHttpUrl } from './urls';

describe('isHttpUrl', () => {
  it('accepts absolute http and https URLs', () => {
    expect(isHttpUrl('https://id.example.com/authorize?client_id=x')).toBe(true);
    expect(isHttpUrl('http://localhost:9090/pairing')).toBe(true);
  });

  it('rejects script, data and other schemes', () => {
    expect(isHttpUrl('javascript:alert(1)')).toBe(false);
    expect(isHttpUrl('JavaScript:alert(1)')).toBe(false);
    expect(isHttpUrl('data:text/html,hi')).toBe(false);
    expect(isHttpUrl('ftp://example.com/file')).toBe(false);
  });

  it('rejects relative, empty and missing values', () => {
    expect(isHttpUrl('/api/auth/oidc/wysch/challenge')).toBe(false);
    expect(isHttpUrl('')).toBe(false);
    expect(isHttpUrl(null)).toBe(false);
    expect(isHttpUrl(undefined)).toBe(false);
  });
});
