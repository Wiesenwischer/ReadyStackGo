import { apiDelete, apiGet, apiPost, apiPut } from './client';

export interface SnmpStatus {
  enabled: boolean;
  port: number;
  listenAddress: string;
  rootOid: string;
  v2cConfigured: boolean;
  v3UserCount: number;
}

export interface OidReferenceScalar {
  symbol: string;
  oid: string;
  type: string;
  currentValue: string;
}

/**
 * One concrete table column for a specific row — fully qualified OID with its
 * MIB name, type and the value SNMP currently returns for it.
 */
export interface OidReferenceColumn {
  symbol: string;
  columnNumber: number;
  oid: string;
  type: string;
  currentValue: string;
}

export interface OidReferenceService {
  serviceIndex: number;
  name: string;
  containerName: string;
  running: boolean;
  columns: OidReferenceColumn[];
}

export interface OidReferenceStack {
  stackIndex: number;
  name: string;
  status: number;
  statusText: string;
  columns: OidReferenceColumn[];
  services: OidReferenceService[];
}

export interface OidReferenceProduct {
  productIndex: number;
  productId: string;
  name: string;
  version: string;
  status: number;
  statusText: string;
  columns: OidReferenceColumn[];
  stacks: OidReferenceStack[];
}

export interface OidReferenceEnvironment {
  environmentIndex: number;
  environmentId: string;
  name: string;
  environmentType: number;
  columns: OidReferenceColumn[];
  products: OidReferenceProduct[];
}

export interface OidReference {
  rootOid: string;
  snmpEnabled: boolean;
  port: number;
  listenAddress: string;
  system: OidReferenceScalar[];
  environments: OidReferenceEnvironment[];
}

export async function getSnmpStatus(): Promise<SnmpStatus> {
  return apiGet<SnmpStatus>('/api/snmp/status');
}

export async function getOidReference(): Promise<OidReference> {
  return apiGet<OidReference>('/api/snmp/oid-reference');
}

export function getMibDownloadUrl(): string {
  return '/api/snmp/mib';
}

export function getPrtgBundleDownloadUrl(): string {
  return '/api/snmp/prtg-bundle';
}

/**
 * Builds the absolute URL a PRTG "HTTP Data Advanced" sensor polls.
 *
 * @param apiKey - API key created via /settings/cicd (any key with `Settings:Read`
 *                  permission works). Pass an empty string to render the URL with
 *                  a placeholder for display purposes only.
 * @param origin - Optional override; defaults to `window.location.origin`.
 */
export function getPrtgSensorUrl(apiKey: string, origin?: string): string {
  const base = (origin ?? (typeof window !== 'undefined' ? window.location.origin : ''))
    .replace(/\/$/, '');
  const keyParam = apiKey ? encodeURIComponent(apiKey) : 'YOUR_API_KEY';
  return `${base}/api/integrations/prtg/status?apikey=${keyParam}`;
}

// ─── Editable settings (v0.65) ───────────────────────────────────────────

export interface SnmpSettings {
  enabled: boolean;
  port: number;
  listenAddress: string;
  rootOid: string;
  community: string;
  trapReceivers: string;
  v3UserCount: number;
}

export type SnmpAuthProtocol = 'None' | 'Md5' | 'Sha1' | 'Sha256' | 'Sha384' | 'Sha512';
export type SnmpPrivProtocol = 'None' | 'Des' | 'Aes128' | 'Aes192' | 'Aes256';

export interface SnmpV3User {
  id: string;
  name: string;
  authProtocol: SnmpAuthProtocol;
  privProtocol: SnmpPrivProtocol;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AddV3UserRequest {
  name: string;
  authProtocol: SnmpAuthProtocol;
  authPassphrase?: string;
  privProtocol: SnmpPrivProtocol;
  privPassphrase?: string;
}

export interface UpdateSnmpSettingsRequest {
  enabled: boolean;
  port: number;
  listenAddress: string;
  rootOid: string;
  community: string;
  trapReceivers: string;
}

export async function getSnmpSettings(): Promise<SnmpSettings> {
  return apiGet<SnmpSettings>('/api/snmp/settings');
}

export async function updateSnmpSettings(req: UpdateSnmpSettingsRequest): Promise<void> {
  await apiPut<void>('/api/snmp/settings', req);
}

export async function listV3Users(): Promise<SnmpV3User[]> {
  return apiGet<SnmpV3User[]>('/api/snmp/v3-users');
}

export async function addV3User(req: AddV3UserRequest): Promise<{ id: string }> {
  return apiPost<{ id: string }>('/api/snmp/v3-users', req);
}

export async function deleteV3User(id: string): Promise<void> {
  await apiDelete<void>(`/api/snmp/v3-users/${id}`);
}
