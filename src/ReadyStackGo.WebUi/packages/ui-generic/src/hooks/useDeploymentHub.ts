import { useAuth } from '../context/AuthContext';
import {
  useDeploymentHub as useCoreDeploymentHub,
  type UseDeploymentHubOptions,
  type UseDeploymentHubReturn,
} from '@rsgo/core';

export type { UseDeploymentHubOptions, UseDeploymentHubReturn };

export function useDeploymentHub(options: UseDeploymentHubOptions = {}): UseDeploymentHubReturn {
  const { token } = useAuth();
  return useCoreDeploymentHub(token, options);
}
