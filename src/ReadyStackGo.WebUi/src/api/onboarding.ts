import { apiGet, apiPost } from './client';

export interface OnboardingItemDto {
  done: boolean;
  count: number;
  name?: string;
}

export interface OnboardingStatusResponse {
  isComplete: boolean;
  isDismissed: boolean;
  organization: OnboardingItemDto;
  environment: OnboardingItemDto;
  stackSources: OnboardingItemDto;
  registries: OnboardingItemDto;
}

export async function getOnboardingStatus(): Promise<OnboardingStatusResponse> {
  return apiGet<OnboardingStatusResponse>('/api/onboarding/status');
}

export async function dismissOnboarding(): Promise<void> {
  return apiPost('/api/onboarding/dismiss');
}
