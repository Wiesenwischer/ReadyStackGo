import { apiGet, apiPost, apiDelete } from './client';

// Deployment DTOs matching the backend
export interface EnvironmentVariableInfo {
  name: string;
  defaultValue?: string;
  isRequired: boolean;
  usedInServices: string[];
}

export interface ParseComposeRequest {
  yamlContent: string;
}

export interface ParseComposeResponse {
  success: boolean;
  message?: string;
  variables: EnvironmentVariableInfo[];
  services: string[];
  errors: string[];
  warnings: string[];
}

export interface DeployComposeRequest {
  stackName: string;
  yamlContent: string;
  /** Version of the stack (from product manifest metadata.productVersion) */
  stackVersion?: string;
  variables: Record<string, string>;
  /** Client-generated session ID for real-time progress tracking via SignalR */
  sessionId?: string;
}

/**
 * Request for deploying a stack from the catalog.
 * Uses stackId instead of raw YAML content.
 */
export interface DeployStackRequest {
  stackName: string;
  variables: Record<string, string>;
  /** Client-generated session ID for real-time progress tracking via SignalR */
  sessionId?: string;
}

/**
 * Response from deploying a stack.
 */
export interface DeployStackResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  stackVersion?: string;
  services: DeployedServiceInfo[];
  errors: string[];
  warnings: string[];
  /** Session ID for real-time progress tracking via SignalR */
  deploymentSessionId?: string;
}

export interface DeployedServiceInfo {
  serviceName: string;
  containerId?: string;
  status?: string;
  ports: string[];
}

export interface DeployComposeResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  services: DeployedServiceInfo[];
  errors: string[];
  warnings: string[];
  /** Session ID for real-time progress tracking via SignalR */
  deploymentSessionId?: string;
}

export interface DeploymentSummary {
  deploymentId?: string;
  stackName: string;
  stackVersion?: string;
  deployedAt: string;
  serviceCount: number;
  status?: string;
}

export interface ListDeploymentsResponse {
  success: boolean;
  deployments: DeploymentSummary[];
}

export interface GetDeploymentResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  environmentId?: string;
  deployedAt?: string;
  services: DeployedServiceInfo[];
  configuration: Record<string, string>;
}

// API functions
export async function parseCompose(request: ParseComposeRequest): Promise<ParseComposeResponse> {
  return apiPost<ParseComposeResponse>('/api/deployments/parse', request);
}

export async function deployCompose(environmentId: string, request: DeployComposeRequest): Promise<DeployComposeResponse> {
  return apiPost<DeployComposeResponse>(`/api/environments/${environmentId}/deployments`, request);
}

/**
 * Deploy a stack from the catalog by stackId.
 * This is the preferred method for deploying catalog stacks.
 */
export async function deployStack(environmentId: string, stackId: string, request: DeployStackRequest): Promise<DeployStackResponse> {
  return apiPost<DeployStackResponse>(`/api/environments/${environmentId}/stacks/${encodeURIComponent(stackId)}/deploy`, request);
}

export async function listDeployments(environmentId: string): Promise<ListDeploymentsResponse> {
  return apiGet<ListDeploymentsResponse>(`/api/environments/${environmentId}/deployments`);
}

export async function getDeployment(environmentId: string, deploymentId: string): Promise<GetDeploymentResponse> {
  return apiGet<GetDeploymentResponse>(`/api/environments/${environmentId}/deployments/${deploymentId}`);
}

export async function removeDeployment(environmentId: string, deploymentId: string): Promise<DeployComposeResponse> {
  return apiDelete<DeployComposeResponse>(`/api/environments/${environmentId}/deployments/${deploymentId}`);
}
