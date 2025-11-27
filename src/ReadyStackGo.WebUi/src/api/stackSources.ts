import { apiGet, apiPost } from './client';

export interface StackVariable {
  name: string;
  defaultValue?: string;
  isRequired: boolean;
}

export interface StackDefinition {
  id: string;
  sourceId: string;
  name: string;
  description?: string;
  services: string[];
  variables: StackVariable[];
  lastSyncedAt: string;
  version?: string;
}

export interface StackSource {
  id: string;
  name: string;
  type: string;
  enabled: boolean;
  lastSyncedAt?: string;
  details: Record<string, string>;
}

export interface SyncResult {
  success: boolean;
  stacksLoaded: number;
  sourcesSynced: number;
  errors: string[];
  warnings: string[];
}

export async function getStackDefinitions(): Promise<StackDefinition[]> {
  return apiGet<StackDefinition[]>('/api/stack-sources/stacks');
}

export async function getStackSources(): Promise<StackSource[]> {
  return apiGet<StackSource[]>('/api/stack-sources');
}

export async function syncSources(): Promise<SyncResult> {
  return apiPost<SyncResult>('/api/stack-sources/sync');
}

export interface StackDefinitionDetail extends StackDefinition {
  yamlContent: string;
  filePath?: string;
  additionalFiles?: string[];
}

export async function getStackDefinitionDetail(stackId: string): Promise<StackDefinitionDetail> {
  return apiGet<StackDefinitionDetail>(`/api/stack-sources/stacks/${encodeURIComponent(stackId)}`);
}
