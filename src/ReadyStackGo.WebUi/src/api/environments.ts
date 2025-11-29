import { apiGet, apiPost, apiPut, apiDelete } from './client';

// Environment DTOs matching the backend
export interface EnvironmentResponse {
  id: string;
  name: string;
  type: string;
  connectionString: string;
  isDefault: boolean;
  createdAt: string;
}

export interface CreateEnvironmentRequest {
  id: string;
  name: string;
  socketPath: string;
}

export interface CreateEnvironmentResponse {
  success: boolean;
  message?: string;
  environment?: EnvironmentResponse;
}

export interface UpdateEnvironmentRequest {
  name: string;
  socketPath: string;
}

export interface UpdateEnvironmentResponse {
  success: boolean;
  message?: string;
  environment?: EnvironmentResponse;
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
