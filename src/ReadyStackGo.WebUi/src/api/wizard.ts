import { apiGet, apiPost } from './client';

// Wizard DTOs matching the backend
// v0.4: ConnectionsSet removed from wizard states
// v0.6: Added timeout tracking with permanent lock

/** Timeout information for the wizard setup window */
export interface WizardTimeoutInfo {
  /** Whether the wizard window has timed out */
  isTimedOut: boolean;
  /** Whether the wizard is permanently locked (requires container restart) */
  isLocked: boolean;
  /** Remaining seconds until timeout. Null if already timed out */
  remainingSeconds: number | null;
  /** When the timeout window expires (UTC ISO string) */
  expiresAt: string | null;
  /** The configured timeout duration in seconds */
  timeoutSeconds: number;
}

export interface WizardStatusResponse {
  wizardState: 'NotStarted' | 'AdminCreated' | 'OrganizationSet' | 'Installed';
  isCompleted: boolean;
  /** Default Docker socket path for the server's OS (e.g., "npipe://./pipe/docker_engine" for Windows) */
  defaultDockerSocketPath: string;
  /** Timeout information for the wizard window (v0.6) */
  timeout?: WizardTimeoutInfo;
}

export interface CreateAdminRequest {
  username: string;
  password: string;
}

export interface CreateAdminResponse {
  success: boolean;
  message?: string;
}

export interface SetOrganizationRequest {
  id: string;
  name: string;
}

export interface SetOrganizationResponse {
  success: boolean;
  message?: string;
}

export interface SetEnvironmentRequest {
  name: string;
  socketPath: string;
}

export interface SetEnvironmentResponse {
  success: boolean;
  message?: string;
  environmentId?: string;
}

/**
 * @deprecated v0.4: Global connection strings are replaced by stack-specific configuration.
 * This type is kept for backwards compatibility and will be removed in v0.5.
 */
export interface SetConnectionsRequest {
  transport: string;
  persistence: string;
  eventStore?: string;
}

/**
 * @deprecated v0.4: Global connection strings are replaced by stack-specific configuration.
 * This type is kept for backwards compatibility and will be removed in v0.5.
 */
export interface SetConnectionsResponse {
  success: boolean;
  message?: string;
}

export interface SetSourcesRequest {
  registrySourceIds: string[];
}

export interface SetSourcesResponse {
  success: boolean;
  message?: string;
  sourcesCreated: number;
}

export interface WizardRegistrySource {
  id: string;
  name: string;
  description: string;
  type: string;  // "git-repository" or "local-directory"
  category: string;
  tags: string[];
  featured: boolean;
  stackCount: number;
}

// Registry detection and configuration (v0.25)
export interface DetectedRegistryArea {
  host: string;
  namespace: string;
  suggestedPattern: string;
  suggestedName: string;
  isLikelyPublic: boolean;
  isConfigured: boolean;
  images: string[];
}

export interface DetectRegistriesResponse {
  areas: DetectedRegistryArea[];
}

export interface RegistryInputDto {
  name: string;
  host: string;
  pattern: string;
  requiresAuth: boolean;
  username?: string;
  password?: string;
}

export interface SetRegistriesRequestDto {
  registries: RegistryInputDto[];
}

export interface SetRegistriesResponseDto {
  success: boolean;
  registriesCreated: number;
  registriesSkipped: number;
}

export interface InstallStackRequest {
  manifestPath?: string;
}

export interface InstallStackResponse {
  success: boolean;
  stackVersion?: string;
  deployedContexts: string[];
  errors: string[];
}

// API functions
export async function getWizardStatus(): Promise<WizardStatusResponse> {
  return apiGet<WizardStatusResponse>('/api/wizard/status');
}

export async function createAdmin(request: CreateAdminRequest): Promise<CreateAdminResponse> {
  return apiPost<CreateAdminResponse>('/api/wizard/admin', request);
}

export async function setOrganization(request: SetOrganizationRequest): Promise<SetOrganizationResponse> {
  return apiPost<SetOrganizationResponse>('/api/wizard/organization', request);
}

export async function setEnvironment(request: SetEnvironmentRequest): Promise<SetEnvironmentResponse> {
  return apiPost<SetEnvironmentResponse>('/api/wizard/environment', request);
}

/**
 * @deprecated v0.4: Global connection strings are replaced by stack-specific configuration.
 * This function is kept for backwards compatibility and will be removed in v0.5.
 */
export async function setConnections(request: SetConnectionsRequest): Promise<SetConnectionsResponse> {
  return apiPost<SetConnectionsResponse>('/api/wizard/connections', request);
}

export async function getWizardRegistry(): Promise<WizardRegistrySource[]> {
  return apiGet<WizardRegistrySource[]>('/api/wizard/registry');
}

export async function setSources(request: SetSourcesRequest): Promise<SetSourcesResponse> {
  return apiPost<SetSourcesResponse>('/api/wizard/sources', request);
}

export async function detectRegistries(): Promise<DetectRegistriesResponse> {
  return apiGet<DetectRegistriesResponse>('/api/wizard/detected-registries');
}

export async function setRegistries(request: SetRegistriesRequestDto): Promise<SetRegistriesResponseDto> {
  return apiPost<SetRegistriesResponseDto>('/api/wizard/registries', request);
}

export async function installStack(request: InstallStackRequest = {}): Promise<InstallStackResponse> {
  return apiPost<InstallStackResponse>('/api/wizard/install', request);
}
