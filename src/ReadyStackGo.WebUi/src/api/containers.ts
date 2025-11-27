import { apiGet, apiPost } from './client';

export interface Container {
  id: string;
  name: string;
  image: string;
  state: string;
  status: string;
  created: string;
  ports: Port[];
  labels?: Record<string, string>;
  healthStatus?: string; // "healthy", "unhealthy", "starting", or "none"
  failingStreak?: number;
}

export interface Port {
  privatePort: number;
  publicPort: number;
  type: string;
}

export const containerApi = {
  async list(environmentId: string): Promise<Container[]> {
    return apiGet<Container[]>(`/api/containers?environment=${encodeURIComponent(environmentId)}`);
  },

  async start(environmentId: string, id: string): Promise<void> {
    return apiPost(`/api/containers/${id}/start?environment=${encodeURIComponent(environmentId)}`);
  },

  async stop(environmentId: string, id: string): Promise<void> {
    return apiPost(`/api/containers/${id}/stop?environment=${encodeURIComponent(environmentId)}`);
  },
};
