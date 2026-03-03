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

// Utils
export * from './utils/timeAgo';
