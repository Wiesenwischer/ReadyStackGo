import { apiGet, apiPost, apiPut, apiDelete } from './client';

// Environment DTOs matching the backend
export interface EnvironmentResponse {
  id: string;
  name: string;
  type: string;
  connectionString: string;
  isDefault: boolean;
  createdAt: string;
  // SSH-specific fields (only set for SshTunnel type)
  sshHost?: string;
  sshPort?: number;
  sshUsername?: string;
  sshAuthMethod?: string;
  remoteSocketPath?: string;
}

export type EnvironmentType = 'DockerSocket' | 'SshTunnel';

export interface CreateEnvironmentRequest {
  name: string;
  type: EnvironmentType;
  // DockerSocket fields
  socketPath?: string;
  // SSH Tunnel fields
  sshHost?: string;
  sshPort?: number;
  sshUsername?: string;
  sshAuthMethod?: string;
  sshSecret?: string;
  remoteSocketPath?: string;
}

export interface CreateEnvironmentResponse {
  success: boolean;
  message?: string;
  environment?: EnvironmentResponse;
}

export interface UpdateEnvironmentRequest {
  name: string;
  type: EnvironmentType;
  socketPath?: string;
  sshHost?: string;
  sshPort?: number;
  sshUsername?: string;
  sshAuthMethod?: string;
  sshSecret?: string;
  remoteSocketPath?: string;
}

export interface UpdateEnvironmentResponse {
  success: boolean;
  message?: string;
  environment?: EnvironmentResponse;
}

export interface TestConnectionRequest {
  type: EnvironmentType;
  dockerHost?: string;
  sshHost?: string;
  sshPort?: number;
  sshUsername?: string;
  sshAuthMethod?: string;
  sshSecret?: string;
  remoteSocketPath?: string;
}

export interface TestConnectionResponseDto {
  success: boolean;
  message: string;
  dockerVersion?: string;
}

export interface DeleteEnvironmentResponse {
  success: boolean;
  message?: string;
}

export interface ListEnvironmentsResponse {
  success: boolean;
  environments: EnvironmentResponse[];
}

export interface SetDefaultEnvironmentResponse {
  success: boolean;
  message?: string;
}

// API functions
export async function getEnvironments(): Promise<ListEnvironmentsResponse> {
  return apiGet<ListEnvironmentsResponse>('/api/environments');
}

export async function getEnvironment(id: string): Promise<EnvironmentResponse> {
  return apiGet<EnvironmentResponse>(`/api/environments/${id}`);
}

export async function createEnvironment(request: CreateEnvironmentRequest): Promise<CreateEnvironmentResponse> {
  return apiPost<CreateEnvironmentResponse>('/api/environments', request);
}

export async function updateEnvironment(id: string, request: UpdateEnvironmentRequest): Promise<UpdateEnvironmentResponse> {
  return apiPut<UpdateEnvironmentResponse>(`/api/environments/${id}`, request);
}

export async function deleteEnvironment(id: string): Promise<DeleteEnvironmentResponse> {
  return apiDelete<DeleteEnvironmentResponse>(`/api/environments/${id}`);
}

export async function setDefaultEnvironment(id: string): Promise<SetDefaultEnvironmentResponse> {
  return apiPost<SetDefaultEnvironmentResponse>(`/api/environments/${id}/default`, {});
}

export async function testConnection(request: TestConnectionRequest): Promise<TestConnectionResponseDto> {
  return apiPost<TestConnectionResponseDto>('/api/environments/test-connection', request);
}

// Environment Variables
export interface GetEnvironmentVariablesResponse {
  variables: Record<string, string>;
}

export interface SaveEnvironmentVariablesRequest {
  variables: Record<string, string>;
}

export interface SaveEnvironmentVariablesResponse {
  success: boolean;
  message?: string;
}

export async function getEnvironmentVariables(environmentId: string): Promise<GetEnvironmentVariablesResponse> {
  return apiGet<GetEnvironmentVariablesResponse>(`/api/environments/${environmentId}/variables`);
}

export async function saveEnvironmentVariables(
  environmentId: string,
  request: SaveEnvironmentVariablesRequest
): Promise<SaveEnvironmentVariablesResponse> {
  return apiPost<SaveEnvironmentVariablesResponse>(`/api/environments/${environmentId}/variables`, request);
}
