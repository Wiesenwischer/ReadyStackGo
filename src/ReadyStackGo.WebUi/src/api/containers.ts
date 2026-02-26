import { apiGet, apiPost, apiDelete } from './client';

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

export interface StackContextInfo {
  stackName: string;
  deploymentExists: boolean;
  deploymentId?: string;
  productName?: string;
  productDisplayName?: string;
  stackDefinitionName?: string;
}

export interface ContainerContextResult {
  success: boolean;
  stacks: Record<string, StackContextInfo>;
  errorMessage?: string;
}

export interface RemoveOrphanedStackResult {
  success: boolean;
  removedCount: number;
  errorMessage?: string;
}

export interface RepairOrphanedStackResult {
  success: boolean;
  deploymentId?: string;
  catalogMatched: boolean;
  errorMessage?: string;
}

export interface RepairAllOrphanedStacksResult {
  success: boolean;
  repairedCount: number;
  failedCount: number;
  errorMessage?: string;
}

export const containerApi = {
  async list(environmentId: string): Promise<Container[]> {
    return apiGet<Container[]>(`/api/containers?environment=${encodeURIComponent(environmentId)}`);
  },

  async getContext(environmentId: string): Promise<ContainerContextResult> {
    return apiGet<ContainerContextResult>(`/api/containers/context?environment=${encodeURIComponent(environmentId)}`);
  },

  async start(environmentId: string, id: string): Promise<void> {
    return apiPost(`/api/containers/${id}/start?environment=${encodeURIComponent(environmentId)}`);
  },

  async stop(environmentId: string, id: string): Promise<void> {
    return apiPost(`/api/containers/${id}/stop?environment=${encodeURIComponent(environmentId)}`);
  },

  async remove(environmentId: string, id: string, force: boolean = false): Promise<void> {
    return apiDelete(`/api/containers/${id}?environment=${encodeURIComponent(environmentId)}&force=${force}`);
  },

  async removeOrphanedStack(environmentId: string, stackName: string): Promise<RemoveOrphanedStackResult> {
    return apiDelete<RemoveOrphanedStackResult>(
      `/api/containers/orphaned-stacks/${encodeURIComponent(stackName)}?environment=${encodeURIComponent(environmentId)}`
    );
  },

  async repairOrphanedStack(environmentId: string, stackName: string): Promise<RepairOrphanedStackResult> {
    return apiPost<RepairOrphanedStackResult>(
      `/api/containers/orphaned-stacks/${encodeURIComponent(stackName)}/repair?environment=${encodeURIComponent(environmentId)}`
    );
  },

  async repairAllOrphanedStacks(environmentId: string): Promise<RepairAllOrphanedStacksResult> {
    return apiPost<RepairAllOrphanedStacksResult>(
      `/api/containers/repair-all-orphaned?environment=${encodeURIComponent(environmentId)}`
    );
  },
};
