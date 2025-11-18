import { apiGet, apiPost } from './client';

export interface Container {
  id: string;
  name: string;
  image: string;
  state: string;
  status: string;
  created: string;
  ports: Port[];
}

export interface Port {
  privatePort: number;
  publicPort: number;
  type: string;
}

export const containerApi = {
  async list(): Promise<Container[]> {
    return apiGet<Container[]>('/api/containers');
  },

  async start(id: string): Promise<void> {
    return apiPost(`/api/containers/${id}/start`);
  },

  async stop(id: string): Promise<void> {
    return apiPost(`/api/containers/${id}/stop`);
  },
};
