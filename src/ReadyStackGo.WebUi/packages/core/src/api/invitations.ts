import { apiGet, apiPost, apiDelete } from './client';

export type InvitationStatus = 'Pending' | 'Accepted' | 'Revoked' | 'Expired';

export interface InvitationDto {
  id: string;
  email: string;
  status: InvitationStatus;
  roleId: string;
  scopeType: string;
  scopeId?: string;
  createdAt: string;
  expiresAt: string;
}

export interface CreateInvitationRequest {
  email: string;
  roleId: string;
  scopeType: string;
  scopeId?: string;
}

export async function listInvitations(): Promise<InvitationDto[]> {
  return apiGet<InvitationDto[]>('/api/invitations');
}

export async function createInvitation(request: CreateInvitationRequest): Promise<InvitationDto> {
  return apiPost<InvitationDto>('/api/invitations', request);
}

export async function revokeInvitation(id: string): Promise<void> {
  return apiDelete<void>(`/api/invitations/${encodeURIComponent(id)}`);
}
