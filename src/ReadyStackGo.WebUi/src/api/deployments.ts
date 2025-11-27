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
  variables: Record<string, string>;
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
  return apiPost<DeployComposeResponse>(`/api/deployments/${environmentId}`, request);
}

export async function listDeployments(environmentId: string): Promise<ListDeploymentsResponse> {
  return apiGet<ListDeploymentsResponse>(`/api/deployments/${environmentId}`);
}

export async function getDeployment(environmentId: string, stackName: string): Promise<GetDeploymentResponse> {
  return apiGet<GetDeploymentResponse>(`/api/deployments/${environmentId}/${stackName}`);
}

export async function removeDeployment(environmentId: string, stackName: string): Promise<DeployComposeResponse> {
  return apiDelete<DeployComposeResponse>(`/api/deployments/${environmentId}/${stackName}`);
}
