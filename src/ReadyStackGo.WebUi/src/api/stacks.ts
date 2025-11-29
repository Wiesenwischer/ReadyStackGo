import { apiGet, apiPost } from './client';

export interface StackVariable {
  name: string;
  defaultValue?: string;
  isRequired: boolean;
}

export interface Stack {
  id: string;
  sourceId: string;
  sourceName: string;
  name: string;
  description?: string;
  relativePath?: string;
  services: string[];
  variables: StackVariable[];
  lastSyncedAt: string;
  version?: string;
}

export interface StackDetail extends Stack {
  yamlContent: string;
  filePath?: string;
  additionalFiles?: string[];
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

// Stack API
export async function getStacks(): Promise<Stack[]> {
  return apiGet<Stack[]>('/api/stacks');
}

export async function getStack(stackId: string): Promise<StackDetail> {
  return apiGet<StackDetail>(`/api/stacks/${encodeURIComponent(stackId)}`);
}

// Stack Sources API (admin)
export async function getStackSources(): Promise<StackSource[]> {
  return apiGet<StackSource[]>('/api/stack-sources');
}

export async function syncSources(): Promise<SyncResult> {
  return apiPost<SyncResult>('/api/stack-sources/sync');
}

// Re-export old names for backwards compatibility during migration
/** @deprecated Use Stack instead */
export type StackDefinition = Stack;
/** @deprecated Use StackDetail instead */
export type StackDefinitionDetail = StackDetail;
/** @deprecated Use getStacks instead */
export const getStackDefinitions = getStacks;
/** @deprecated Use getStack instead */
export const getStackDefinitionDetail = getStack;
