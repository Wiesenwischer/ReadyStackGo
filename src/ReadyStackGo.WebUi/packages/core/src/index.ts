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
export { useTlsStore, type UseTlsStoreReturn, type ConfigureLetsEncryptResult } from './hooks/useTlsStore';
export { useApiKeyStore, type UseApiKeyStoreReturn } from './hooks/useApiKeyStore';
export { useRemoveStackStore, type UseRemoveStackStoreReturn, type RemoveState } from './hooks/useRemoveStackStore';
export { useRollbackStore, type UseRollbackStoreReturn, type RollbackState } from './hooks/useRollbackStore';
export { useDeployStackStore, type UseDeployStackStoreReturn, type DeployState } from './hooks/useDeployStackStore';
export { useUpgradeStackStore, type UseUpgradeStackStoreReturn, type UpgradeState } from './hooks/useUpgradeStackStore';
export { useRemoveProductStore, type UseRemoveProductStoreReturn, type RemoveProductState } from './hooks/useRemoveProductStore';
export { useRetryProductStore, type UseRetryProductStoreReturn, type RetryProductState } from './hooks/useRetryProductStore';
export { useRedeployProductStore, type UseRedeployProductStoreReturn, type RedeployProductState } from './hooks/useRedeployProductStore';
export { useDeploymentsStore, type UseDeploymentsStoreReturn } from './hooks/useDeploymentsStore';
export { useRestartProductStore, type UseRestartProductStoreReturn, type RestartProductState } from './hooks/useRestartProductStore';
export { useStopProductStore, type UseStopProductStoreReturn, type StopProductState } from './hooks/useStopProductStore';
export { useProductDeploymentDetailStore, type UseProductDeploymentDetailStoreReturn, type ProductDeploymentDetailState } from './hooks/useProductDeploymentDetailStore';
export { useDeploymentDetailStore, type UseDeploymentDetailStoreReturn } from './hooks/useDeploymentDetailStore';
export { useDeployProductStore, type UseDeployProductStoreReturn, type DeployProductState } from './hooks/useDeployProductStore';
export { useUpgradeProductStore, type UseUpgradeProductStoreReturn, type UpgradeProductState } from './hooks/useUpgradeProductStore';
export { useHealthDashboardStore, type UseHealthDashboardStoreReturn, type StatusFilter, type ProductGroup } from './hooks/useHealthDashboardStore';
export { useContainerStore, type UseContainerStoreReturn, type ViewMode, type OrphanConfirm, type StackGroup, type ProductGrouping } from './hooks/useContainerStore';
export { useContainerLogsStore, type UseContainerLogsStoreReturn, type TailOption } from './hooks/useContainerLogsStore';
export { useVolumeStore, type UseVolumeStoreReturn } from './hooks/useVolumeStore';
export { useVolumeDetailStore, type UseVolumeDetailStoreReturn, type VolumeDeleteState } from './hooks/useVolumeDetailStore';
export { useServiceHealthDetailStore, type UseServiceHealthDetailStoreReturn } from './hooks/useServiceHealthDetailStore';
export { useDashboardStore, type UseDashboardStoreReturn } from './hooks/useDashboardStore';
export { useCatalogStore, type UseCatalogStoreReturn } from './hooks/useCatalogStore';
export { useProductDetailStore, type UseProductDetailStoreReturn } from './hooks/useProductDetailStore';
export { useEnvironmentStore, type UseEnvironmentStoreReturn } from './hooks/useEnvironmentStore';
export { useProfileStore, type UseProfileStoreReturn } from './hooks/useProfileStore';
export { useSystemSettingsStore, type UseSystemSettingsStoreReturn } from './hooks/useSystemSettingsStore';
export { useUpdateStore, type UseUpdateStoreReturn, type UpdatePhase } from './hooks/useUpdateStore';
export { useWizardStore, type UseWizardStoreReturn } from './hooks/useWizardStore';
export { useOnboardingStore, type UseOnboardingStoreReturn } from './hooks/useOnboardingStore';
export { useOnboardingOrgStore, type UseOnboardingOrgStoreReturn } from './hooks/useOnboardingOrgStore';
export { useOnboardingEnvStore, type UseOnboardingEnvStoreReturn } from './hooks/useOnboardingEnvStore';
export { useOnboardingSourcesStore, type UseOnboardingSourcesStoreReturn } from './hooks/useOnboardingSourcesStore';
export { useRegistriesStepStore, type UseRegistriesStepStoreReturn, type RegistryCardState, type CardStatus, type VerifyStatus, type InitialCheckStatus } from './hooks/useRegistriesStepStore';
export { useHealthWidgetStore, type UseHealthWidgetStoreReturn, type ProductHealthGroup } from './hooks/useHealthWidgetStore';
export { useSetupHintStore, type UseSetupHintStoreReturn } from './hooks/useSetupHintStore';
export { useConnectionTestStore, type UseConnectionTestStoreReturn, type TestConnectionResponse } from './hooks/useConnectionTestStore';
export { useHealthHistoryStore, type UseHealthHistoryStoreReturn } from './hooks/useHealthHistoryStore';
export { useSetupOrganizationStore, type UseSetupOrganizationStoreReturn } from './hooks/useSetupOrganizationStore';

// Services
export * from './services/AuthService';
export * from './services/EnvironmentService';

// Utils
export * from './utils/timeAgo';
