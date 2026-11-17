import { apiGet } from './client';
import type { ProductReleaseNotesResponse } from './deployments';

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
  defaultTransient?: boolean;
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

/**
 * Service definition in a stack.
 */
export interface ServiceDetail {
  name: string;
  image: string;
  containerName?: string;
  ports: string[];
  environment: Record<string, string>;
  volumes: string[];
  networks: string[];
  dependsOn: string[];
}

/**
 * Named volume definition.
 */
export interface VolumeDetail {
  name: string;
  driver?: string;
  external: boolean;
}

/**
 * Network definition.
 */
export interface NetworkDetail {
  name: string;
  driver?: string;
  external: boolean;
}

/**
 * Detailed stack information with structured service data.
 * v0.12: Replaced yamlContent with structured services, volumes, networks.
 * Note: services is ServiceDetail[] (detailed), overriding the base string[] (names only)
 */
export interface StackDetail extends Omit<Stack, 'services'> {
  /** Full service definitions with all details */
  services: ServiceDetail[];
  /** Named volumes defined in the stack */
  volumes: VolumeDetail[];
  /** Networks defined in the stack */
  networks: NetworkDetail[];
  filePath?: string;
  /** Product ID for navigation back to catalog (format: sourceId:productName) */
  productId: string;
}

// Stack API
export async function getStacks(): Promise<Stack[]> {
  return apiGet<Stack[]>('/api/stacks');
}

export async function getStack(stackId: string): Promise<StackDetail> {
  return apiGet<StackDetail>(`/api/stacks/${encodeURIComponent(stackId)}`);
}

// Product API (grouped stacks)
export interface ProductStack {
  id: string;
  name: string;
  description?: string;
  services: string[];
  variables: StackVariable[];
}

/**
 * Information about a specific product version.
 */
export interface ProductVersion {
  version: string;
  productId: string;
  defaultStackId: string;
  isCurrent: boolean;
  /** Whether this version has release notes (a CHANGELOG or an external URL) to display. */
  hasReleaseNotes?: boolean;
}

export interface Product {
  id: string;
  groupId: string;
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
  availableVersions?: ProductVersion[];
}

export async function getProducts(): Promise<Product[]> {
  return apiGet<Product[]>('/api/products');
}

export async function getProduct(productId: string): Promise<Product> {
  return apiGet<Product>(`/api/products/${encodeURIComponent(productId)}`);
}

/**
 * Fetch release notes for a catalog product version directly by its product id (no
 * deployment required). Pass `locale` (e.g. "de", "en") to select a localized changelog.
 */
export async function getCatalogProductReleaseNotes(
  productId: string,
  locale?: string
): Promise<ProductReleaseNotesResponse> {
  const localeParam = locale ? `?locale=${encodeURIComponent(locale)}` : '';
  return apiGet<ProductReleaseNotesResponse>(
    `/api/products/${encodeURIComponent(productId)}/release-notes${localeParam}`
  );
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
