import { apiGet, apiPost, apiDelete } from './client';

export interface Volume {
  name: string;
  driver: string;
  mountpoint?: string;
  scope?: string;
  createdAt?: string;
  labels: Record<string, string>;
  containerCount: number;
  referencedByContainers: string[];
  isOrphaned: boolean;
  sizeBytes?: number;
}

export interface CreateVolumeRequest {
  name: string;
  driver?: string;
  labels?: Record<string, string>;
}

export const volumeApi = {
  async list(environmentId: string): Promise<Volume[]> {
    return apiGet<Volume[]>(`/api/volumes?environment=${encodeURIComponent(environmentId)}`);
  },

  async get(environmentId: string, name: string): Promise<Volume> {
    return apiGet<Volume>(`/api/volumes/${encodeURIComponent(name)}?environment=${encodeURIComponent(environmentId)}`);
  },

  async create(environmentId: string, request: CreateVolumeRequest): Promise<Volume> {
    return apiPost<Volume>(`/api/volumes?environment=${encodeURIComponent(environmentId)}`, request);
  },

  async remove(environmentId: string, name: string, force: boolean = false): Promise<void> {
    return apiDelete(`/api/volumes/${encodeURIComponent(name)}?environment=${encodeURIComponent(environmentId)}&force=${force}`);
  },
};
