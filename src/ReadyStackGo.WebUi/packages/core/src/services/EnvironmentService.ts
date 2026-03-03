// Framework-agnostic Environment Service
// Handles loading environments from API, active environment selection, and localStorage persistence.

import { getEnvironments, type EnvironmentResponse } from '../api/environments';

const ACTIVE_ENV_KEY = 'rsgo_active_environment';

export interface EnvironmentState {
  environments: EnvironmentResponse[];
  activeEnvironment: EnvironmentResponse | null;
}

/** Load all environments from the API. */
export async function loadEnvironments(): Promise<EnvironmentResponse[]> {
  const response = await getEnvironments();
  if (response.success) {
    return response.environments;
  }
  throw new Error('Failed to load environments: response.success is false');
}

/** Get the stored active environment ID from localStorage. */
export function getStoredActiveEnvironmentId(): string | null {
  return localStorage.getItem(ACTIVE_ENV_KEY);
}

/** Persist the active environment ID to localStorage. */
export function setStoredActiveEnvironmentId(id: string): void {
  localStorage.setItem(ACTIVE_ENV_KEY, id);
}

/**
 * Resolve which environment should be active, using this priority:
 * 1. Previously stored ID (from localStorage)
 * 2. The default environment (isDefault flag)
 * 3. The first environment in the list
 */
export function resolveActiveEnvironment(
  environments: EnvironmentResponse[],
  storedId: string | null = getStoredActiveEnvironmentId()
): EnvironmentResponse | null {
  if (environments.length === 0) return null;

  let active: EnvironmentResponse | undefined;

  if (storedId) {
    active = environments.find(e => e.id === storedId);
  }

  if (!active) {
    active = environments.find(e => e.isDefault);
  }

  if (!active) {
    active = environments[0];
  }

  if (active) {
    setStoredActiveEnvironmentId(active.id);
  }

  return active ?? null;
}

/**
 * Select a specific environment by ID from the given list.
 * Persists the selection to localStorage.
 * Returns the environment if found, null otherwise.
 */
export function selectEnvironment(
  environments: EnvironmentResponse[],
  id: string
): EnvironmentResponse | null {
  const env = environments.find(e => e.id === id);
  if (env) {
    setStoredActiveEnvironmentId(id);
  }
  return env ?? null;
}
