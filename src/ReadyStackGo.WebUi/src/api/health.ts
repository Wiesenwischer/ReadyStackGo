import { apiGet, apiPut } from './client';

// Types for health data (matching backend DTOs)
export interface StackHealthDto {
  deploymentId: string;
  environmentId: string;
  stackName: string;
  currentVersion: string | null;
  targetVersion: string | null;
  overallStatus: string;
  operationMode: string;
  statusMessage: string;
  requiresAttention: boolean;
  capturedAtUtc: string;
  self: SelfHealthDto;
  bus: BusHealthDto | null;
  infra: InfraHealthDto | null;
}

export interface SelfHealthDto {
  status: string;
  healthyCount: number;
  totalCount: number;
  services: ServiceHealthDto[];
}

export interface ServiceHealthDto {
  name: string;
  status: string;
  containerId: string | null;
  containerName: string | null;
  reason: string | null;
  restartCount: number;
}

export interface BusHealthDto {
  status: string;
  transportKey: string | null;
  hasCriticalError: boolean;
  criticalErrorMessage: string | null;
  lastHealthPingProcessedUtc: string | null;
  endpoints: BusEndpointHealthDto[];
}

export interface BusEndpointHealthDto {
  endpointName: string;
  status: string;
  lastPingUtc: string | null;
  reason: string | null;
}

export interface InfraHealthDto {
  status: string;
  databases: DatabaseHealthDto[];
  disks: DiskHealthDto[];
  externalServices: ExternalServiceHealthDto[];
}

export interface DatabaseHealthDto {
  id: string;
  status: string;
  latencyMs: number | null;
  error: string | null;
}

export interface DiskHealthDto {
  mount: string;
  status: string;
  freePercent: number | null;
  error: string | null;
}

export interface ExternalServiceHealthDto {
  id: string;
  status: string;
  error: string | null;
  responseTimeMs: number | null;
}

export interface EnvironmentHealthSummaryDto {
  environmentId: string;
  environmentName: string;
  totalStacks: number;
  healthyCount: number;
  degradedCount: number;
  unhealthyCount: number;
  stacks: StackHealthSummaryDto[];
}

export interface StackHealthSummaryDto {
  deploymentId: string;
  stackName: string;
  currentVersion: string | null;
  overallStatus: string;
  operationMode: string;
  healthyServices: number;
  totalServices: number;
  statusMessage: string;
  requiresAttention: boolean;
  capturedAtUtc: string;
}

// API Response wrapper types
interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

interface HistoryResponse {
  success: boolean;
  message?: string;
  history: StackHealthSummaryDto[];
}

// API functions
export async function getStackHealth(
  environmentId: string,
  deploymentId: string,
  forceRefresh: boolean = false
): Promise<StackHealthDto> {
  const url = `/api/health/${environmentId}/deployments/${deploymentId}${forceRefresh ? '?forceRefresh=true' : ''}`;
  const response = await apiGet<ApiResponse<StackHealthDto>>(url);
  if (!response.success || !response.data) {
    throw new Error(response.message || 'Failed to get stack health');
  }
  return response.data;
}

export async function getEnvironmentHealthSummary(
  environmentId: string
): Promise<EnvironmentHealthSummaryDto> {
  const url = `/api/health/${environmentId}`;
  const response = await apiGet<ApiResponse<EnvironmentHealthSummaryDto>>(url);
  if (!response.success || !response.data) {
    throw new Error(response.message || 'Failed to get environment health summary');
  }
  return response.data;
}

export async function getHealthHistory(
  deploymentId: string,
  limit: number = 50
): Promise<StackHealthSummaryDto[]> {
  const url = `/api/health/deployments/${deploymentId}/history?limit=${limit}`;
  const response = await apiGet<HistoryResponse>(url);
  if (!response.success) {
    throw new Error(response.message || 'Failed to get health history');
  }
  return response.history;
}

// UI Presentation helpers - maps status/mode to visual presentation
export type HealthStatusName = 'Healthy' | 'Degraded' | 'Unhealthy' | 'Unknown';
export type OperationModeName = 'Normal' | 'Migrating' | 'Maintenance' | 'Stopped' | 'Failed';

export interface StatusPresentation {
  color: string;
  bgColor: string;
  textColor: string;
  icon: string;
  label: string;
}

export function getHealthStatusPresentation(status: string): StatusPresentation {
  switch (status.toLowerCase()) {
    case 'healthy':
      return {
        color: 'green',
        bgColor: 'bg-green-100',
        textColor: 'text-green-800',
        icon: 'check-circle',
        label: 'Healthy'
      };
    case 'degraded':
      return {
        color: 'yellow',
        bgColor: 'bg-yellow-100',
        textColor: 'text-yellow-800',
        icon: 'alert-triangle',
        label: 'Degraded'
      };
    case 'unhealthy':
      return {
        color: 'red',
        bgColor: 'bg-red-100',
        textColor: 'text-red-800',
        icon: 'x-circle',
        label: 'Unhealthy'
      };
    default:
      return {
        color: 'gray',
        bgColor: 'bg-gray-100',
        textColor: 'text-gray-800',
        icon: 'help-circle',
        label: 'Unknown'
      };
  }
}

export function getOperationModePresentation(mode: string): StatusPresentation {
  switch (mode.toLowerCase()) {
    case 'normal':
      return {
        color: 'green',
        bgColor: 'bg-green-100',
        textColor: 'text-green-800',
        icon: 'play',
        label: 'Normal'
      };
    case 'migrating':
      return {
        color: 'blue',
        bgColor: 'bg-blue-100',
        textColor: 'text-blue-800',
        icon: 'refresh-cw',
        label: 'Migrating'
      };
    case 'maintenance':
      return {
        color: 'yellow',
        bgColor: 'bg-yellow-100',
        textColor: 'text-yellow-800',
        icon: 'tool',
        label: 'Maintenance'
      };
    case 'stopped':
      return {
        color: 'gray',
        bgColor: 'bg-gray-100',
        textColor: 'text-gray-600',
        icon: 'square',
        label: 'Stopped'
      };
    case 'failed':
      return {
        color: 'red',
        bgColor: 'bg-red-100',
        textColor: 'text-red-800',
        icon: 'alert-octagon',
        label: 'Failed'
      };
    default:
      return {
        color: 'gray',
        bgColor: 'bg-gray-100',
        textColor: 'text-gray-800',
        icon: 'help-circle',
        label: mode
      };
  }
}

// Helper to check if status needs attention
export function statusRequiresAttention(status: string): boolean {
  const lowerStatus = status.toLowerCase();
  return lowerStatus === 'degraded' || lowerStatus === 'unhealthy';
}

// Helper to format health ratio
export function formatHealthRatio(healthy: number, total: number): string {
  return `${healthy}/${total}`;
}

// Helper to calculate health percentage
export function calculateHealthPercentage(healthy: number, total: number): number {
  if (total === 0) return 0;
  return Math.round((healthy / total) * 100);
}

// Operation Mode API

export interface ChangeOperationModeRequest {
  mode: OperationModeName;
  reason?: string;
  targetVersion?: string;
}

export interface ChangeOperationModeResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  previousMode?: string;
  newMode?: string;
}

/**
 * Change the operation mode of a deployment.
 * @param environmentId The environment ID
 * @param deploymentId The deployment ID
 * @param request The mode change request
 */
export async function changeOperationMode(
  environmentId: string,
  deploymentId: string,
  request: ChangeOperationModeRequest
): Promise<ChangeOperationModeResponse> {
  return apiPut<ChangeOperationModeResponse>(
    `/api/environments/${environmentId}/deployments/${deploymentId}/operation-mode`,
    request
  );
}

/**
 * Enter maintenance mode for a deployment.
 */
export async function enterMaintenanceMode(
  environmentId: string,
  deploymentId: string,
  reason?: string
): Promise<ChangeOperationModeResponse> {
  return changeOperationMode(environmentId, deploymentId, { mode: 'Maintenance', reason });
}

/**
 * Exit maintenance mode and return to normal operation.
 */
export async function exitMaintenanceMode(
  environmentId: string,
  deploymentId: string
): Promise<ChangeOperationModeResponse> {
  return changeOperationMode(environmentId, deploymentId, { mode: 'Normal' });
}
