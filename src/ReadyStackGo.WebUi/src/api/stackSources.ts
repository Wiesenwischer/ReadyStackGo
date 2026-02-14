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
  gitSslVerify?: boolean;
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
  sslVerify?: boolean;
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

/**
 * A curated stack source entry from the embedded registry.
 */
export interface RegistrySourceDto {
  id: string;
  name: string;
  description: string;
  type: string;  // "git-repository" or "local-directory"
  gitUrl: string;
  gitBranch: string;
  path?: string;
  filePattern?: string;
  category: string;
  tags: string[];
  featured: boolean;
  stackCount: number;
  alreadyAdded: boolean;
}

// Stack Sources API

/**
 * Get all entries from the curated source registry.
 */
export async function getRegistrySources(): Promise<RegistrySourceDto[]> {
  return apiGet<RegistrySourceDto[]>('/api/stack-sources/registry');
}


/**
 * Add a stack source from the curated registry.
 */
export async function addFromRegistry(registrySourceId: string): Promise<StackSourceResponse> {
  return apiPost<StackSourceResponse>('/api/stack-sources/from-registry', { registrySourceId });
}

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
 * Export all stack source configurations as JSON.
 */
export async function exportSources(): Promise<ExportSourcesResponse> {
  return apiGet<ExportSourcesResponse>('/api/stack-sources/export');
}

/**
 * Import stack source configurations from JSON.
 */
export async function importSources(data: ImportSourcesRequest): Promise<ImportSourcesResponse> {
  return apiPost<ImportSourcesResponse>('/api/stack-sources/import', data);
}

export interface ExportSourcesResponse {
  version: string;
  exportedAt: string;
  sources: ExportedSourceDto[];
}

export interface ExportedSourceDto {
  name: string;
  type: string;
  enabled: boolean;
  path?: string;
  filePattern?: string;
  gitUrl?: string;
  gitBranch?: string;
  gitSslVerify?: boolean;
}

export interface ImportSourcesRequest {
  version: string;
  sources: ExportedSourceDto[];
}

export interface ImportSourcesResponse {
  success: boolean;
  message?: string;
  sourcesCreated: number;
  sourcesSkipped: number;
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
