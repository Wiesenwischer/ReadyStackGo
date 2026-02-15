using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Onboarding.GetOnboardingStatus;

namespace ReadyStackGo.API.Endpoints.Onboarding;

/// <summary>
/// GET /api/onboarding/status - Get onboarding checklist status
/// </summary>
public class GetOnboardingStatusEndpoint : EndpointWithoutRequest<OnboardingStatusResponse>
{
    private readonly IMediator _mediator;

    public GetOnboardingStatusEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/onboarding/status");
        Description(b => b.WithTags("Onboarding"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOnboardingStatusQuery(), ct);

        Response = new OnboardingStatusResponse
        {
            IsComplete = result.IsComplete,
            IsDismissed = result.IsDismissed,
            Organization = MapItem(result.Organization),
            Environment = MapItem(result.Environment),
            StackSources = MapItem(result.StackSources),
            Registries = MapItem(result.Registries)
        };
    }

    private static OnboardingItemDto MapItem(OnboardingItemStatus item) => new()
    {
        Done = item.Done,
        Count = item.Count,
        Name = item.Name
    };
}

public class OnboardingStatusResponse
{
    public bool IsComplete { get; set; }
    public bool IsDismissed { get; set; }
    public required OnboardingItemDto Organization { get; set; }
    public required OnboardingItemDto Environment { get; set; }
    public required OnboardingItemDto StackSources { get; set; }
    public required OnboardingItemDto Registries { get; set; }
}

public class OnboardingItemDto
{
    public bool Done { get; set; }
    public int Count { get; set; }
    public string? Name { get; set; }
}
