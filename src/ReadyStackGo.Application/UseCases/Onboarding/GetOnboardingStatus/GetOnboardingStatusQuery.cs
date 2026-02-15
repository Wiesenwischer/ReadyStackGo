using MediatR;

namespace ReadyStackGo.Application.UseCases.Onboarding.GetOnboardingStatus;

public record GetOnboardingStatusQuery : IRequest<OnboardingStatusResult>;

public record OnboardingItemStatus(bool Done, int Count, string? Name = null);

public record OnboardingStatusResult(
    bool IsComplete,
    bool IsDismissed,
    OnboardingItemStatus Organization,
    OnboardingItemStatus Environment,
    OnboardingItemStatus StackSources,
    OnboardingItemStatus Registries);
