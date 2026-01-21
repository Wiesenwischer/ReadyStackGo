import { apiGet, apiPost, apiPut, apiDelete } from './client';

/**
 * Stack source configuration.
 */
export interface StackSourceDto {
  id: string;
  name: string;
  type: string;  // "LocalDirectory" or "GitRepository"
  enabled: boolean;
  lastSyncedAt?: string;
  details: Record<string, string>;
}

/**
 * Detailed stack source information.
 */
export interface StackSourceDetailDto {
  id: string;
  name: string;
  type: string;
  enabled: boolean;
  lastSyncedAt?: string;
  createdAt: string;
  path?: string;
  filePattern?: string;
  gitUrl?: string;
  gitBranch?: string;
  gitUsername?: string;
  hasGitPassword: boolean;
}

/**
 * Request for creating a stack source.
 */
export interface CreateStackSourceRequest {
  id: string;
  name: string;
  type: 'LocalDirectory' | 'GitRepository';
  // For LocalDirectory
  path?: string;
  filePattern?: string;
  // For GitRepository
  gitUrl?: string;
  branch?: string;
  gitUsername?: string;
  gitPassword?: string;
}

/**
 * Request for updating a stack source.
 */
export interface UpdateStackSourceRequest {
  name?: string;
  enabled?: boolean;
}

/**
 * Response for stack source operations.
 */
export interface StackSourceResponse {
  success: boolean;
  message?: string;
  sourceId?: string;
}

/**
 * Response for sync operations.
 */
export interface SyncResult {
  success: boolean;
  stacksLoaded: number;
  sourcesSynced: number;
  errors: string[];
  warnings: string[];
}

// Stack Sources API

/**
 * Get all stack sources.
 */
export async function getStackSources(): Promise<StackSourceDto[]> {
  return apiGet<StackSourceDto[]>('/api/stack-sources');
}

/**
 * Get a specific stack source by ID.
 */
export async function getStackSource(id: string): Promise<StackSourceDetailDto> {
  return apiGet<StackSourceDetailDto>(`/api/stack-sources/${encodeURIComponent(id)}`);
}

/**
 * Create a new stack source.
 */
export async function createStackSource(request: CreateStackSourceRequest): Promise<StackSourceResponse> {
  return apiPost<StackSourceResponse>('/api/stack-sources', request);
}

/**
 * Update a stack source.
 */
export async function updateStackSource(id: string, request: UpdateStackSourceRequest): Promise<StackSourceResponse> {
  return apiPut<StackSourceResponse>(`/api/stack-sources/${encodeURIComponent(id)}`, request);
}

/**
 * Delete a stack source.
 */
export async function deleteStackSource(id: string): Promise<StackSourceResponse> {
  return apiDelete<StackSourceResponse>(`/api/stack-sources/${encodeURIComponent(id)}`);
}

/**
 * Sync all stack sources.
 */
export async function syncAllSources(): Promise<SyncResult> {
  return apiPost<SyncResult>('/api/stack-sources/sync');
}

/**
 * Sync a specific stack source.
 */
export async function syncSource(id: string): Promise<SyncResult> {
  return apiPost<SyncResult>(`/api/stack-sources/${encodeURIComponent(id)}/sync`);
}
