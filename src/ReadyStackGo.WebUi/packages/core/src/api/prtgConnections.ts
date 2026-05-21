import { apiDelete, apiGet, apiPost, apiPut } from './client';

export interface PrtgConnectionDto {
  id: string;
  name: string;
  url: string;
  hasApiToken: boolean;        // never receives the actual token back
  templateDeviceId: number | null;
  verifyTls: boolean;
  createdAt: string;
  updatedAt: string | null;
  lastUsedAt: string | null;
}

export interface CreatePrtgConnectionRequest {
  name: string;
  url: string;
  apiToken: string;            // plaintext, encrypted server-side
  templateDeviceId: number | null;
  verifyTls: boolean;
}

export interface UpdatePrtgConnectionRequest {
  name: string;
  url: string;
  apiToken?: string | null;    // null/empty = keep existing
  templateDeviceId: number | null;
  verifyTls: boolean;
}

export interface PrtgConnectionResponse {
  success: boolean;
  error?: string;
  connection?: PrtgConnectionDto;
}

export async function listPrtgConnections(): Promise<PrtgConnectionDto[]> {
  return apiGet<PrtgConnectionDto[]>('/api/prtg-connections');
}

export async function getPrtgConnection(id: string): Promise<PrtgConnectionDto> {
  return apiGet<PrtgConnectionDto>(`/api/prtg-connections/${id}`);
}

export async function createPrtgConnection(req: CreatePrtgConnectionRequest): Promise<PrtgConnectionResponse> {
  return apiPost<PrtgConnectionResponse>('/api/prtg-connections', req);
}

export async function updatePrtgConnection(id: string, req: UpdatePrtgConnectionRequest): Promise<PrtgConnectionResponse> {
  return apiPut<PrtgConnectionResponse>(`/api/prtg-connections/${id}`, { id, ...req });
}

export async function deletePrtgConnection(id: string): Promise<void> {
  await apiDelete<void>(`/api/prtg-connections/${id}`);
}

export async function linkProductDeploymentToPrtgConnection(
  productDeploymentId: string,
  prtgConnectionId: string | null,
): Promise<void> {
  await apiPut<void>(`/api/deployments/${productDeploymentId}/prtg-connection`, {
    id: productDeploymentId,
    prtgConnectionId,
  });
}

/**
 * Sets ad-hoc per-deployment PRTG credentials (Variant 2). Pass null URL to clear.
 */
export interface InlinePrtgRegistration {
  url: string | null;
  apiToken?: string | null;
  templateDeviceId?: number | null;
  verifyTls: boolean;
}

export async function setInlinePrtgRegistration(
  productDeploymentId: string,
  registration: InlinePrtgRegistration,
): Promise<void> {
  await apiPut<void>(`/api/deployments/${productDeploymentId}/prtg-inline`, {
    id: productDeploymentId,
    url: registration.url,
    apiToken: registration.apiToken ?? null,
    templateDeviceId: registration.templateDeviceId ?? null,
    verifyTls: registration.verifyTls,
  });
}
