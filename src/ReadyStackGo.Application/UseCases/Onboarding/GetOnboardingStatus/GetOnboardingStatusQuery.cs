using MediatR;

namespace ReadyStackGo.Application.UseCases.Onboarding.GetOnboardingStatus;

public record GetOnboardingStatusQuery : IRequest<OnboardingStatusResult>;

public record OnboardingItemStatus(bool Done, int Count, string? Name = null);

/// <summary>
/// A single step from the wizard definition provider, enriched with completion status.
/// </summary>
public record OnboardingStepStatus(
    string Id,
    string Title,
    string Description,
    string ComponentType,
    bool Required,
    int Order,
    bool Done,
    int Count);

public record OnboardingStatusResult(
    bool IsComplete,
    bool IsDismissed,
    OnboardingItemStatus Organization,
    OnboardingItemStatus Environment,
    OnboardingItemStatus StackSources,
    OnboardingItemStatus Registries,
    string DistributionId = "generic",
    IReadOnlyList<OnboardingStepStatus>? Steps = null);
