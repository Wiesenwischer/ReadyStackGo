import { apiPost } from './client';

export interface CreateOrganizationRequest {
  id?: string;
  name: string;
}

export interface CreateOrganizationResponse {
  success: boolean;
  organizationId?: string;
}

export async function createOrganization(request: CreateOrganizationRequest): Promise<CreateOrganizationResponse> {
  return apiPost<CreateOrganizationResponse>('/api/organizations', request);
}
