import { apiGet } from './client';

export interface OnboardingItemDto {
  done: boolean;
  count: number;
  name?: string;
}

export interface OnboardingStepDto {
  id: string;
  title: string;
  description: string;
  componentType: string;
  required: boolean;
  order: number;
  done: boolean;
  count: number;
}

export interface OnboardingStatusResponse {
  isComplete: boolean;
  isDismissed: boolean;
  organization: OnboardingItemDto;
  environment: OnboardingItemDto;
  stackSources: OnboardingItemDto;
  registries: OnboardingItemDto;
  distributionId: string;
  steps?: OnboardingStepDto[];
}

export async function getOnboardingStatus(): Promise<OnboardingStatusResponse> {
  return apiGet<OnboardingStatusResponse>('/api/onboarding/status');
}
