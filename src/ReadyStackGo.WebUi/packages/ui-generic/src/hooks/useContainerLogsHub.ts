import { useAuth } from '../context/AuthContext';
import {
  useContainerLogsHub as useCoreContainerLogsHub,
  type UseContainerLogsHubOptions,
  type UseContainerLogsHubReturn,
} from '@rsgo/core';

export type { UseContainerLogsHubOptions, UseContainerLogsHubReturn };

export function useContainerLogsHub(options: UseContainerLogsHubOptions = {}): UseContainerLogsHubReturn {
  const { token } = useAuth();
  return useCoreContainerLogsHub(token, options);
}
