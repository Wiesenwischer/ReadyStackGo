import { apiGet, apiPost, apiDelete } from './client';

export interface Stack {
  id: string;
  name: string;
  description: string;
  services: StackService[];
  status: string;
  deployedAt?: string;
  updatedAt?: string;
}

export interface StackService {
  name: string;
  image: string;
  ports?: string[];
  environment?: Record<string, string>;
  volumes?: string[];
  containerId?: string;
  containerStatus?: string;
}

export const stackApi = {
  async list(): Promise<Stack[]> {
    return apiGet<Stack[]>('/api/stacks');
  },

  async get(id: string): Promise<Stack> {
    return apiGet<Stack>(`/api/stacks/${id}`);
  },

  async deploy(id: string): Promise<Stack> {
    return apiPost<Stack>(`/api/stacks/${id}/deploy`, {});
  },

  async remove(id: string): Promise<void> {
    return apiDelete(`/api/stacks/${id}`);
  },
};
