import { apiGet, apiPost, apiDelete } from './client';

// Deployment DTOs matching the backend
export interface EnvironmentVariableInfo {
  name: string;
  defaultValue?: string;
  isRequired: boolean;
  usedInServices: string[];
}

export interface ParseComposeRequest {
  yamlContent: string;
}

export interface ParseComposeResponse {
  success: boolean;
  message?: string;
  variables: EnvironmentVariableInfo[];
  services: string[];
  errors: string[];
  warnings: string[];
}

export interface DeployComposeRequest {
  stackName: string;
  yamlContent: string;
  /** Version of the stack (from product manifest metadata.productVersion) */
  stackVersion?: string;
  variables: Record<string, string>;
  /** Client-generated session ID for real-time progress tracking via SignalR */
  sessionId?: string;
}

/**
 * Request for deploying a stack from the catalog.
 * Uses stackId instead of raw YAML content.
 */
export interface DeployStackRequest {
  stackName: string;
  variables: Record<string, string>;
  /** Client-generated session ID for real-time progress tracking via SignalR */
  sessionId?: string;
}

/**
 * Response from deploying a stack.
 */
export interface DeployStackResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  stackVersion?: string;
  services: DeployedServiceInfo[];
  errors: string[];
  warnings: string[];
  /** Session ID for real-time progress tracking via SignalR */
  deploymentSessionId?: string;
}

export interface DeployedServiceInfo {
  serviceName: string;
  containerId?: string;
  status?: string;
  ports: string[];
}

export interface DeployComposeResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  services: DeployedServiceInfo[];
  errors: string[];
  warnings: string[];
  /** Session ID for real-time progress tracking via SignalR */
  deploymentSessionId?: string;
}

export interface DeploymentSummary {
  deploymentId?: string;
  stackName: string;
  stackVersion?: string;
  deployedAt: string;
  serviceCount: number;
  status?: string;
}

export interface ListDeploymentsResponse {
  success: boolean;
  deployments: DeploymentSummary[];
}

export interface InitContainerResultDto {
  serviceName: string;
  success: boolean;
  exitCode: number;
  executedAtUtc: string;
  logOutput?: string;
}

export interface GetDeploymentResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  stackVersion?: string;
  environmentId?: string;
  deployedAt?: string;
  /** Current deployment status: Installing, Upgrading, Running, Failed, Removed */
  status?: string;
  services: DeployedServiceInfo[];
  initContainerResults: InitContainerResultDto[];
  configuration: Record<string, string>;
}

// API functions
export async function parseCompose(request: ParseComposeRequest): Promise<ParseComposeResponse> {
  return apiPost<ParseComposeResponse>('/api/deployments/parse', request);
}

export async function deployCompose(environmentId: string, request: DeployComposeRequest): Promise<DeployComposeResponse> {
  return apiPost<DeployComposeResponse>(`/api/environments/${environmentId}/deployments`, request);
}

/**
 * Deploy a stack from the catalog by stackId.
 * This is the preferred method for deploying catalog stacks.
 */
export async function deployStack(environmentId: string, stackId: string, request: DeployStackRequest): Promise<DeployStackResponse> {
  return apiPost<DeployStackResponse>(`/api/environments/${environmentId}/stacks/${encodeURIComponent(stackId)}/deploy`, request);
}

export async function listDeployments(environmentId: string): Promise<ListDeploymentsResponse> {
  return apiGet<ListDeploymentsResponse>(`/api/environments/${environmentId}/deployments`);
}

export async function getDeployment(environmentId: string, deploymentId: string): Promise<GetDeploymentResponse> {
  return apiGet<GetDeploymentResponse>(`/api/environments/${environmentId}/deployments/${deploymentId}`);
}

export interface RemoveDeploymentRequest {
  sessionId?: string;
}

export async function removeDeployment(
  environmentId: string,
  deploymentId: string,
  request?: RemoveDeploymentRequest
): Promise<DeployComposeResponse> {
  return apiDelete<DeployComposeResponse>(
    `/api/environments/${environmentId}/deployments/${deploymentId}`,
    request
  );
}

// ============================================================================
// Rollback API
// ============================================================================

/**
 * Response from GET /api/environments/{envId}/deployments/{deploymentId}/snapshots
 * Contains rollback information for a deployment.
 */
export interface RollbackInfoResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  stackName?: string;
  currentVersion?: string;
  /** Whether rollback is currently available (Failed status + has PendingUpgradeSnapshot) */
  canRollback: boolean;
  /** The version that would be restored on rollback */
  rollbackTargetVersion?: string;
  /** When the snapshot was created (start of upgrade) */
  snapshotCreatedAt?: string;
  /** Description of the snapshot */
  snapshotDescription?: string;
  /** Legacy field - contains 0 or 1 snapshot for backwards compatibility */
  snapshots: SnapshotDto[];
}

export interface SnapshotDto {
  snapshotId: string;
  stackVersion: string;
  createdAt: string;
  description?: string;
  serviceCount: number;
  variableCount: number;
}

/**
 * Response from POST /api/environments/{envId}/deployments/{deploymentId}/rollback
 */
export interface RollbackResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  targetVersion?: string;
  previousVersion?: string;
}

/**
 * Get rollback information for a deployment.
 * Returns information about the pending upgrade snapshot and whether rollback is available.
 */
export async function getRollbackInfo(environmentId: string, deploymentId: string): Promise<RollbackInfoResponse> {
  return apiGet<RollbackInfoResponse>(`/api/environments/${environmentId}/deployments/${deploymentId}/snapshots`);
}

/**
 * Request for triggering a rollback.
 */
export interface RollbackRequest {
  /** Optional client session ID for SignalR progress tracking */
  sessionId?: string;
}

/**
 * Trigger rollback to the previous version.
 * Only available when deployment status is Failed and has a PendingUpgradeSnapshot.
 * No snapshotId needed - automatically uses the single PendingUpgradeSnapshot.
 */
export async function rollbackDeployment(
  environmentId: string,
  deploymentId: string,
  request?: RollbackRequest
): Promise<RollbackResponse> {
  return apiPost<RollbackResponse>(
    `/api/environments/${environmentId}/deployments/${deploymentId}/rollback`,
    request ?? {}
  );
}

// ============================================================================
// Upgrade API
// ============================================================================

/**
 * Available version for upgrade
 */
export interface AvailableVersion {
  /** Version string (e.g., "2.0.0") */
  version: string;
  /** Stack ID for this version (for use in upgrade request) */
  stackId: string;
  /** Source ID where this version is available */
  sourceId: string;
}

/**
 * Response from GET /api/environments/{envId}/deployments/{deploymentId}/upgrade/check
 */
export interface CheckUpgradeResponse {
  success: boolean;
  message?: string;
  /** Whether an upgrade is available (newer version exists in catalog) */
  upgradeAvailable: boolean;
  /** Currently deployed version */
  currentVersion?: string;
  /** Latest available version in the catalog */
  latestVersion?: string;
  /** Stack ID of the latest version (for use in upgrade request) */
  latestStackId?: string;
  /** All available upgrade versions, sorted by version (newest first) */
  availableVersions?: AvailableVersion[];
  /** Variables that are new in the latest version */
  newVariables?: string[];
  /** Variables that were removed in the latest version */
  removedVariables?: string[];
  /** Whether the deployment can be upgraded (must be Running status) */
  canUpgrade: boolean;
  /** Reason why upgrade is not available (if canUpgrade is false) */
  cannotUpgradeReason?: string;
}

/**
 * Request for upgrading a deployment.
 */
export interface UpgradeRequest {
  /** Catalog stack ID of the new version */
  stackId: string;
  /** Optional variable overrides */
  variables?: Record<string, string>;
  /** Optional client session ID for SignalR progress tracking */
  sessionId?: string;
}

/**
 * Response from POST /api/environments/{envId}/deployments/{deploymentId}/upgrade
 */
export interface UpgradeResponse {
  success: boolean;
  message?: string;
  deploymentId?: string;
  previousVersion?: string;
  newVersion?: string;
  snapshotId?: string;
  errors?: string[];
  /** Whether rollback is available (only if upgrade failed before container start) */
  canRollback: boolean;
  /** Version to rollback to (if canRollback is true) */
  rollbackVersion?: string;
}

/**
 * Check if an upgrade is available for a deployment.
 */
export async function checkUpgrade(environmentId: string, deploymentId: string): Promise<CheckUpgradeResponse> {
  return apiGet<CheckUpgradeResponse>(`/api/environments/${environmentId}/deployments/${deploymentId}/upgrade/check`);
}

/**
 * Upgrade a deployment to a new version.
 * Only available when deployment status is Running.
 */
export async function upgradeDeployment(
  environmentId: string,
  deploymentId: string,
  request: UpgradeRequest
): Promise<UpgradeResponse> {
  return apiPost<UpgradeResponse>(`/api/environments/${environmentId}/deployments/${deploymentId}/upgrade`, request);
}

// ============================================================================
// Mark Deployment Failed API
// ============================================================================

/**
 * Request for marking a deployment as failed.
 */
export interface MarkDeploymentFailedRequest {
  /** Optional reason for marking as failed */
  reason?: string;
}

/**
 * Response from POST /api/environments/{envId}/deployments/{deploymentId}/mark-failed
 */
export interface MarkDeploymentFailedResponse {
  success: boolean;
  message?: string;
  previousStatus?: string;
}

/**
 * Mark a stuck deployment as failed.
 * Only available when deployment status is Installing or Upgrading.
 */
export async function markDeploymentFailed(
  environmentId: string,
  deploymentId: string,
  reason?: string
): Promise<MarkDeploymentFailedResponse> {
  return apiPost<MarkDeploymentFailedResponse>(
    `/api/environments/${environmentId}/deployments/${deploymentId}/mark-failed`,
    { reason }
  );
}

// ============================================================================
// Product Deployment API
// ============================================================================

/**
 * Per-stack configuration for product deployment.
 */
export interface DeployProductStackConfigRequest {
  stackId: string;
  deploymentStackName: string;
  variables: Record<string, string>;
}

/**
 * Request for deploying an entire product (all stacks).
 */
export interface DeployProductRequest {
  productId: string;
  stackConfigs: DeployProductStackConfigRequest[];
  sharedVariables: Record<string, string>;
  /** Client-generated session ID for real-time progress tracking via SignalR */
  sessionId?: string;
  /** Whether to continue deploying remaining stacks if one fails (default: true) */
  continueOnError?: boolean;
}

/**
 * Result of deploying a single stack within a product deployment.
 */
export interface DeployProductStackResult {
  stackName: string;
  stackDisplayName: string;
  success: boolean;
  deploymentId?: string;
  deploymentStackName?: string;
  errorMessage?: string;
  serviceCount: number;
}

/**
 * Response from deploying an entire product.
 */
export interface DeployProductResponse {
  success: boolean;
  message?: string;
  productDeploymentId?: string;
  productName?: string;
  productVersion?: string;
  status?: string;
  sessionId?: string;
  stackResults: DeployProductStackResult[];
}

/**
 * Deploy an entire product (all stacks) as a single unit.
 */
export async function deployProduct(
  environmentId: string,
  request: DeployProductRequest
): Promise<DeployProductResponse> {
  return apiPost<DeployProductResponse>(
    `/api/environments/${environmentId}/product-deployments`,
    request
  );
}
