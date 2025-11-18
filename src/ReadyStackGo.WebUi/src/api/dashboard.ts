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
  getStats: () => apiGet<DashboardStats>('/api/dashboard/stats'),
};
