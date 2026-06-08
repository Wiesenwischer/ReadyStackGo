// In development, use empty string to use Vite proxy
// In production, API is served from same origin (built into wwwroot)
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '';

function getAuthHeaders(includeContentType: boolean = true): HeadersInit {
  const token = localStorage.getItem('auth_token');
  const headers: HeadersInit = {};

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  if (includeContentType) {
    headers['Content-Type'] = 'application/json';
  }

  return headers;
}

/**
 * Extracts a useful error message from a non-OK fetch response. Tries the JSON
 * body first (FastEndpoints validation errors, business error fields), falls
 * back to statusText.
 */
async function extractErrorMessage(response: Response): Promise<string> {
  const fallback = `API request failed: ${response.statusText}`;
  try {
    const body = await response.json();
    if (body.errors) {
      // FastEndpoints validation error format: { errors: { fieldA: ["msg"], ... } }
      const messages = Object.values(body.errors).flat();
      return messages.length > 0 ? messages.join(', ') : fallback;
    }
    if (body.error) return body.error;
    if (body.message) return body.message;
    if (body.detail) return body.detail;
    if (body.title) return body.title;
    return fallback;
  } catch {
    return fallback;
  }
}

function handle401IfNeeded(response: Response): void {
  if (response.status === 401) {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
    window.location.href = '/login';
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: getAuthHeaders(false),
    cache: 'no-store',
  });

  if (!response.ok) {
    handle401IfNeeded(response);
    throw new Error(await extractErrorMessage(response));
  }

  // Handle empty responses
  const contentLength = response.headers.get('content-length');
  if (response.status === 204 || contentLength === '0') {
    return undefined as T;
  }

  return response.json();
}

export async function apiPost<T = void>(path: string, body?: unknown): Promise<T> {
  // Only include Content-Type header when there's a body to send
  // FastEndpoints will fail to parse JSON if Content-Type is set but body is empty
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: getAuthHeaders(!!body),
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    handle401IfNeeded(response);
    throw new Error(await extractErrorMessage(response));
  }

  // Handle empty responses - check multiple conditions
  const contentLength = response.headers.get('content-length');
  if (response.status === 204 || contentLength === '0') {
    return undefined as T;
  }

  // For responses that might have empty body without content-length header
  const text = await response.text();
  if (!text || text.trim() === '') {
    return undefined as T;
  }

  // Parse the text as JSON
  return JSON.parse(text) as T;
}

export async function apiPut<T = void>(path: string, body?: unknown): Promise<T> {
  // Only include Content-Type header when there's a body to send
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'PUT',
    headers: getAuthHeaders(!!body),
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    handle401IfNeeded(response);
    throw new Error(await extractErrorMessage(response));
  }

  // Handle empty responses - check multiple conditions
  const contentLength = response.headers.get('content-length');
  if (response.status === 204 || contentLength === '0') {
    return undefined as T;
  }

  // For responses that might have empty body without content-length header
  const text = await response.text();
  if (!text || text.trim() === '') {
    return undefined as T;
  }

  // Parse the text as JSON
  return JSON.parse(text) as T;
}

export async function apiDelete<T = void>(path: string, body?: unknown): Promise<T> {
  const hasBody = body !== undefined;
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'DELETE',
    headers: getAuthHeaders(hasBody),
    body: hasBody ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    handle401IfNeeded(response);
    throw new Error(await extractErrorMessage(response));
  }

  // Handle empty responses - check multiple conditions
  const contentLength = response.headers.get('content-length');
  if (response.status === 204 || contentLength === '0') {
    return undefined as T;
  }

  // For responses that might have empty body without content-length header
  const text = await response.text();
  if (!text || text.trim() === '') {
    return undefined as T;
  }

  // Parse the text as JSON
  return JSON.parse(text) as T;
}
