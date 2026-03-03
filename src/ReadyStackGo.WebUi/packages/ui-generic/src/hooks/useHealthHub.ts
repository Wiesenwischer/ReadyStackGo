import { useAuth } from '../context/AuthContext';
import {
  useHealthHub as useCoreHealthHub,
  type UseHealthHubOptions,
  type UseHealthHubReturn,
} from '@rsgo/core';

export type { UseHealthHubOptions, UseHealthHubReturn };

export function useHealthHub(options: UseHealthHubOptions = {}): UseHealthHubReturn {
  const { token } = useAuth();
  return useCoreHealthHub(token, options);
}
