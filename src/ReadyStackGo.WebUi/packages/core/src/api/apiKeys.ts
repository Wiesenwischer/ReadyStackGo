import { apiGet, apiPost, apiDelete } from './client';

export interface ApiKeyDto {
  id: string;
  name: string;
  keyPrefix: string;
  organizationId: string;
  environmentId: string | null;
  permissions: string[];
  createdAt: string;
  lastUsedAt: string | null;
  expiresAt: string | null;
  isRevoked: boolean;
}

export interface ApiKeyCreatedDto {
  id: string;
  name: string;
  keyPrefix: string;
  fullKey: string;
}

export interface CreateApiKeyRequest {
  name: string;
  environmentId?: string;
  permissions: string[];
  expiresAt?: string;
}

export interface CreateApiKeyResponse {
  success: boolean;
  message?: string;
  apiKey?: ApiKeyCreatedDto;
}

export interface ListApiKeysResponse {
  apiKeys: ApiKeyDto[];
}

export interface RevokeApiKeyResponse {
  success: boolean;
  message?: string;
}

export async function getApiKeys(): Promise<ListApiKeysResponse> {
  return apiGet<ListApiKeysResponse>('/api/api-keys');
}

export async function createApiKey(request: CreateApiKeyRequest): Promise<CreateApiKeyResponse> {
  return apiPost<CreateApiKeyResponse>('/api/api-keys', request);
}

export async function revokeApiKey(id: string, reason?: string): Promise<RevokeApiKeyResponse> {
  return apiDelete<RevokeApiKeyResponse>(`/api/api-keys/${id}`, reason ? { reason } : undefined);
}
