import { apiGet } from './client';

export interface DashboardStats {
  /** Total number of products in the catalog */
  totalProducts: number;
  /** Total number of stacks (variants) in the catalog */
  totalStacks: number;
  /** Number of active deployments */
  deployedStacks: number;
  /** Number of stacks not deployed */
  notDeployedStacks: number;
  totalContainers: number;
  runningContainers: number;
  stoppedContainers: number;
  errorMessage?: string;
}

export const dashboardApi = {
  getStats: (environmentId: string) =>
    apiGet<DashboardStats>(`/api/dashboard/stats?environment=${encodeURIComponent(environmentId)}`),
};
