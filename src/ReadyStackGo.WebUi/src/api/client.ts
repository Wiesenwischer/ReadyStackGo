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

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: getAuthHeaders(false),
  });

  if (!response.ok) {
    if (response.status === 401) {
      // Unauthorized - redirect to login
      window.location.href = '/login';
    }
    // Try to get error details from response body
    let errorMessage = `API request failed: ${response.statusText}`;
    try {
      const errorBody = await response.json();
      if (errorBody.errors) {
        // FastEndpoints validation error format
        const messages = Object.values(errorBody.errors).flat();
        errorMessage = messages.join(', ') || errorMessage;
      } else if (errorBody.error) {
        errorMessage = errorBody.error;
      } else if (errorBody.message) {
        errorMessage = errorBody.message;
      }
    } catch {
      // Ignore if body is not JSON
    }
    throw new Error(errorMessage);
  }

  // Handle empty responses
  const contentLength = response.headers.get('content-length');
  if (response.status === 204 || contentLength === '0') {
    return undefined as T;
  }

  return response.json();
}

export async function apiPost<T = void>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    if (response.status === 401) {
      // Unauthorized - redirect to login
      window.location.href = '/login';
    }
    // Try to get error details from response body
    let errorMessage = `API request failed: ${response.statusText}`;
    try {
      const errorBody = await response.json();
      if (errorBody.errors) {
        // FastEndpoints validation error format
        const messages = Object.values(errorBody.errors).flat();
        errorMessage = messages.join(', ') || errorMessage;
      } else if (errorBody.message) {
        errorMessage = errorBody.message;
      }
    } catch {
      // Ignore if body is not JSON
    }
    throw new Error(errorMessage);
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
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    if (response.status === 401) {
      // Unauthorized - redirect to login
      window.location.href = '/login';
    }
    throw new Error(`API request failed: ${response.statusText}`);
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

export async function apiDelete<T = void>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'DELETE',
    headers: getAuthHeaders(false),
  });

  if (!response.ok) {
    if (response.status === 401) {
      // Unauthorized - redirect to login
      window.location.href = '/login';
    }
    throw new Error(`API request failed: ${response.statusText}`);
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
