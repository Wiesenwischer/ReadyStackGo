import { apiPost } from './client';

// ============================================================================
// Deployment Precheck API
// ============================================================================

export interface RunPrecheckRequest {
  stackName: string;
  variables: Record<string, string>;
}

export interface PrecheckCheckDto {
  rule: string;
  severity: 'ok' | 'warning' | 'error';
  title: string;
  detail?: string;
  serviceName?: string;
}

export interface PrecheckResponse {
  canDeploy: boolean;
  hasErrors: boolean;
  hasWarnings: boolean;
  summary: string;
  checks: PrecheckCheckDto[];
}

/**
 * Run deployment precheck for a stack.
 * Returns check results without starting a deployment.
 */
export async function runPrecheck(
  environmentId: string,
  stackId: string,
  request: RunPrecheckRequest
): Promise<PrecheckResponse> {
  return apiPost<PrecheckResponse>(
    `/api/environments/${environmentId}/stacks/${encodeURIComponent(stackId)}/precheck`,
    request
  );
}

// ============================================================================
// Product Deployment Precheck API
// ============================================================================

export interface ProductPrecheckStackConfig {
  stackId: string;
  variables: Record<string, string>;
}

export interface RunProductPrecheckRequest {
  productId: string;
  deploymentName: string;
  stackConfigs: ProductPrecheckStackConfig[];
  sharedVariables: Record<string, string>;
}

export interface ProductPrecheckStackResult {
  stackId: string;
  stackName: string;
  canDeploy: boolean;
  hasErrors: boolean;
  hasWarnings: boolean;
  summary: string;
  checks: PrecheckCheckDto[];
}

export interface ProductPrecheckResponse {
  canDeploy: boolean;
  hasErrors: boolean;
  hasWarnings: boolean;
  summary: string;
  stacks: ProductPrecheckStackResult[];
}

/**
 * Run deployment precheck for all stacks in a product.
 * Returns check results per stack without starting a deployment.
 */
export async function runProductPrecheck(
  environmentId: string,
  request: RunProductPrecheckRequest
): Promise<ProductPrecheckResponse> {
  return apiPost<ProductPrecheckResponse>(
    `/api/environments/${environmentId}/product-deployments/precheck`,
    request
  );
}
