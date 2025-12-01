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

/**
 * @deprecated v0.4: Global connection strings are replaced by stack-specific configuration.
 * This function is kept for backwards compatibility and will be removed in v0.5.
 */
export async function setConnections(request: SetConnectionsRequest): Promise<SetConnectionsResponse> {
  return apiPost<SetConnectionsResponse>('/api/wizard/connections', request);
}

export async function installStack(request: InstallStackRequest = {}): Promise<InstallStackResponse> {
  return apiPost<InstallStackResponse>('/api/wizard/install', request);
}
