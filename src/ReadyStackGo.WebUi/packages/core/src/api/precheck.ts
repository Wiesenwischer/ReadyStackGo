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
