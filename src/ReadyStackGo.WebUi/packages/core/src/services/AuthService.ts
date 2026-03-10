// Framework-agnostic Auth Service
// Handles login/logout API calls and token persistence in localStorage.

const AUTH_TOKEN_KEY = 'auth_token';
const AUTH_USER_KEY = 'auth_user';

export interface AuthUser {
  username: string;
  role: string;
}

export interface AuthState {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
}

interface LoginResponse {
  token: string;
  username: string;
  role: string;
}

function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL || '';
}

/** Check if a JWT token is expired (with 30s grace period). */
function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    // exp is in seconds, Date.now() in ms — add 30s buffer
    return payload.exp * 1000 < Date.now() + 30_000;
  } catch {
    return true;
  }
}

/** Read stored auth from localStorage. Returns unauthenticated state if nothing stored or token expired. */
export function getStoredAuth(): AuthState {
  const token = localStorage.getItem(AUTH_TOKEN_KEY);
  const userJson = localStorage.getItem(AUTH_USER_KEY);

  if (token && userJson) {
    try {
      if (isTokenExpired(token)) {
        clearStoredAuth();
        return { user: null, token: null, isAuthenticated: false };
      }
      const user: AuthUser = JSON.parse(userJson);
      return { user, token, isAuthenticated: true };
    } catch {
      // Corrupted data — clear and return unauthenticated
      clearStoredAuth();
    }
  }

  return { user: null, token: null, isAuthenticated: false };
}

/** Persist auth state to localStorage. */
function persistAuth(token: string, user: AuthUser): void {
  localStorage.setItem(AUTH_TOKEN_KEY, token);
  localStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

/** Clear all auth data from localStorage. */
function clearStoredAuth(): void {
  localStorage.removeItem(AUTH_TOKEN_KEY);
  localStorage.removeItem(AUTH_USER_KEY);
}

/** Authenticate with username/password. Persists the token on success. */
export async function login(username: string, password: string): Promise<AuthState> {
  const response = await fetch(`${getApiBaseUrl()}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    throw new Error('Login failed');
  }

  const data: LoginResponse = await response.json();
  const user: AuthUser = { username: data.username, role: data.role };

  persistAuth(data.token, user);

  return { user, token: data.token, isAuthenticated: true };
}

/** Set auth state directly (e.g. from wizard setup). Persists to localStorage. */
export function setAuthDirectly(token: string, username: string, role: string): AuthState {
  const user: AuthUser = { username, role };
  persistAuth(token, user);
  return { user, token, isAuthenticated: true };
}

/** Logout: clear local state and fire-and-forget the server-side logout. */
export function logout(): AuthState {
  clearStoredAuth();

  // Fire-and-forget server-side logout
  fetch(`${getApiBaseUrl()}/api/auth/logout`, { method: 'POST' }).catch(() => {
    // Ignore errors on logout
  });

  return { user: null, token: null, isAuthenticated: false };
}
