import { apiGet, apiPost } from './client';

/**
 * Variable types matching the backend VariableType enum.
 */
export type VariableType =
  | 'String'
  | 'Number'
  | 'Boolean'
  | 'Select'
  | 'Password'
  | 'Port'
  | 'Url'
  | 'Email'
  | 'Path'
  | 'MultiLine'
  | 'ConnectionString'
  | 'SqlServerConnectionString'
  | 'PostgresConnectionString'
  | 'MySqlConnectionString'
  | 'EventStoreConnectionString'
  | 'MongoConnectionString'
  | 'RedisConnectionString';

/**
 * Option for Select type variables.
 */
export interface SelectOption {
  value: string;
  label?: string;
  description?: string;
}

export interface StackVariable {
  name: string;
  defaultValue?: string;
  isRequired: boolean;
  type?: VariableType;
  label?: string;
  description?: string;
  pattern?: string;
  patternError?: string;
  options?: SelectOption[];
  min?: number;
  max?: number;
  placeholder?: string;
  group?: string;
  order?: number;
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

// Product API (grouped stacks)
export interface ProductStack {
  id: string;
  name: string;
  description?: string;
  services: string[];
  variables: StackVariable[];
}

export interface Product {
  id: string;
  sourceId: string;
  sourceName: string;
  name: string;
  description?: string;
  version?: string;
  category?: string;
  tags?: string[];
  isMultiStack: boolean;
  totalServices: number;
  totalVariables: number;
  stacks: ProductStack[];
  lastSyncedAt: string;
}

export async function getProducts(): Promise<Product[]> {
  return apiGet<Product[]>('/api/products');
}

export async function getProduct(productId: string): Promise<Product> {
  return apiGet<Product>(`/api/products/${encodeURIComponent(productId)}`);
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
