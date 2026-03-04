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
            Registries = MapItem(result.Registries),
            DistributionId = result.DistributionId,
            Steps = result.Steps?.Select(s => new OnboardingStepDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                ComponentType = s.ComponentType,
                Required = s.Required,
                Order = s.Order,
                Done = s.Done,
                Count = s.Count
            }).ToList()
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
    public string DistributionId { get; set; } = "generic";
    public List<OnboardingStepDto>? Steps { get; set; }
}

public class OnboardingItemDto
{
    public bool Done { get; set; }
    public int Count { get; set; }
    public string? Name { get; set; }
}

public class OnboardingStepDto
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string ComponentType { get; set; }
    public bool Required { get; set; }
    public int Order { get; set; }
    public bool Done { get; set; }
    public int Count { get; set; }
}
