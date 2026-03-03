// @rsgo/core - Public API

// API client
export { apiGet, apiPost, apiPut, apiDelete } from './api/client';

// Domain APIs
export * from './api/apiKeys';
export * from './api/containers';
export * from './api/dashboard';
export * from './api/deployments';
export * from './api/environments';
export * from './api/health';
export * from './api/notifications';
export * from './api/onboarding';
export * from './api/organizations';
export * from './api/registries';
export * from './api/stackSources';
export * from './api/stacks';
export * from './api/system';
export * from './api/user';
export * from './api/volumes';
export * from './api/wizard';

// Realtime (SignalR hubs)
// ConnectionState is defined identically in all three authenticated hubs — export once
export type { ConnectionState } from './realtime/useDeploymentHub';
export {
  useContainerLogsHub,
  type UseContainerLogsHubOptions,
  type UseContainerLogsHubReturn,
} from './realtime/useContainerLogsHub';
export {
  useDeploymentHub,
  type DeploymentProgressUpdate,
  type InitContainerLogEntry,
  type UseDeploymentHubOptions,
  type UseDeploymentHubReturn,
} from './realtime/useDeploymentHub';
export {
  useHealthHub,
  type UseHealthHubOptions,
  type UseHealthHubReturn,
} from './realtime/useHealthHub';
export * from './realtime/useUpdateHub';

// Hooks
export * from './hooks/useNotifications';
export * from './hooks/useVersionInfo';

// Store Hooks (ViewModel layer)
export { useRegistryStore, type UseRegistryStoreReturn } from './hooks/useRegistryStore';
export { useStackSourceStore, type UseStackSourceStoreReturn } from './hooks/useStackSourceStore';
export { useTlsStore, type UseTlsStoreReturn } from './hooks/useTlsStore';
export { useApiKeyStore, type UseApiKeyStoreReturn } from './hooks/useApiKeyStore';
export { useRemoveStackStore, type UseRemoveStackStoreReturn, type RemoveState } from './hooks/useRemoveStackStore';
export { useRollbackStore, type UseRollbackStoreReturn, type RollbackState } from './hooks/useRollbackStore';
export { useDeployStackStore, type UseDeployStackStoreReturn, type DeployState } from './hooks/useDeployStackStore';
export { useUpgradeStackStore, type UseUpgradeStackStoreReturn, type UpgradeState } from './hooks/useUpgradeStackStore';
export { useRemoveProductStore, type UseRemoveProductStoreReturn, type RemoveProductState } from './hooks/useRemoveProductStore';
export { useRetryProductStore, type UseRetryProductStoreReturn, type RetryProductState } from './hooks/useRetryProductStore';
export { useRedeployProductStore, type UseRedeployProductStoreReturn, type RedeployProductState } from './hooks/useRedeployProductStore';

// Services
export * from './services/AuthService';
export * from './services/EnvironmentService';

// Utils
export * from './utils/timeAgo';
