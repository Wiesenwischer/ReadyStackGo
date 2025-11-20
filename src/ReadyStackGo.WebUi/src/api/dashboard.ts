import { apiGet } from './client';

export interface DashboardStats {
  totalStacks: number;
  deployedStacks: number;
  notDeployedStacks: number;
  totalContainers: number;
  runningContainers: number;
  stoppedContainers: number;
}

export const dashboardApi = {
  getStats: (environmentId: string) =>
    apiGet<DashboardStats>(`/api/dashboard/stats?environment=${encodeURIComponent(environmentId)}`),
};
