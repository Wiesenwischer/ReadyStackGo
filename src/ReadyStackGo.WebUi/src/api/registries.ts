import { apiGet, apiPost, apiPut, apiDelete } from './client';

// Registry DTOs matching the backend
export interface RegistryDto {
  id: string;
  name: string;
  url: string;
  username: string | null;
  hasCredentials: boolean;
  isDefault: boolean;
  imagePatterns: string[];
  createdAt: string;
  updatedAt: string | null;
}

export interface RegistryResponse {
  success: boolean;
  message?: string;
  registry?: RegistryDto;
}

export interface ListRegistriesResponse {
  registries: RegistryDto[];
}

export interface CreateRegistryRequest {
  name: string;
  url: string;
  username?: string;
  password?: string;
  imagePatterns?: string[];
}

export interface UpdateRegistryRequest {
  name?: string;
  url?: string;
  username?: string;
  password?: string;
  clearCredentials?: boolean;
  imagePatterns?: string[];
}

// API functions
export async function getRegistries(): Promise<ListRegistriesResponse> {
  return apiGet<ListRegistriesResponse>('/api/registries');
}

export async function getRegistry(id: string): Promise<RegistryResponse> {
  return apiGet<RegistryResponse>(`/api/registries/${id}`);
}

export async function createRegistry(request: CreateRegistryRequest): Promise<RegistryResponse> {
  return apiPost<RegistryResponse>('/api/registries', request);
}

export async function updateRegistry(id: string, request: UpdateRegistryRequest): Promise<RegistryResponse> {
  return apiPut<RegistryResponse>(`/api/registries/${id}`, request);
}

export async function deleteRegistry(id: string): Promise<RegistryResponse> {
  return apiDelete<RegistryResponse>(`/api/registries/${id}`);
}

export async function setDefaultRegistry(id: string): Promise<RegistryResponse> {
  return apiPost<RegistryResponse>(`/api/registries/${id}/default`, {});
}
