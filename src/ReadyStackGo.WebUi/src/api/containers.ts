const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5259';

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
    const response = await fetch(`${API_BASE_URL}/api/containers`);
    if (!response.ok) {
      throw new Error('Failed to fetch containers');
    }
    return response.json();
  },

  async start(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/api/containers/${id}/start`, {
      method: 'POST',
    });
    if (!response.ok) {
      throw new Error('Failed to start container');
    }
  },

  async stop(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/api/containers/${id}/stop`, {
      method: 'POST',
    });
    if (!response.ok) {
      throw new Error('Failed to stop container');
    }
  },
};
