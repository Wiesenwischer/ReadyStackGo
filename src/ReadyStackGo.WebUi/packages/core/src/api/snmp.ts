import { apiGet } from './client';

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

export interface OidReferenceService {
  serviceIndex: number;
  name: string;
  containerName: string;
  running: boolean;
  baseOid: string;
}

export interface OidReferenceStack {
  stackIndex: number;
  name: string;
  status: number;
  statusText: string;
  baseOid: string;
  services: OidReferenceService[];
}

export interface OidReferenceProduct {
  productIndex: number;
  productId: string;
  name: string;
  version: string;
  status: number;
  statusText: string;
  baseOid: string;
  stacks: OidReferenceStack[];
}

export interface OidReferenceEnvironment {
  environmentIndex: number;
  environmentId: string;
  name: string;
  environmentType: number;
  baseOid: string;
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
