using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Wizard.DetectRegistries;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// GET /api/wizard/detected-registries - Detect container registries needed by loaded stacks.
/// Returns registry areas grouped by host + namespace with configuration status.
/// Anonymous access (wizard runs before login).
/// </summary>
public class DetectRegistriesEndpoint : EndpointWithoutRequest<DetectRegistriesResponse>
{
    private readonly IMediator _mediator;

    public DetectRegistriesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/wizard/detected-registries");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new DetectRegistriesQuery(), ct);

        Response = new DetectRegistriesResponse
        {
            Areas = result.Areas.Select(a => new DetectedRegistryAreaDto
            {
                Host = a.Host,
                Namespace = a.Namespace,
                SuggestedPattern = a.SuggestedPattern,
                SuggestedName = a.SuggestedName,
                IsLikelyPublic = a.IsLikelyPublic,
                IsConfigured = a.IsConfigured,
                Images = a.Images
            }).ToList()
        };
    }
}

public class DetectRegistriesResponse
{
    public IReadOnlyList<DetectedRegistryAreaDto> Areas { get; init; } = [];
}

public class DetectedRegistryAreaDto
{
    public required string Host { get; init; }
    public required string Namespace { get; init; }
    public required string SuggestedPattern { get; init; }
    public required string SuggestedName { get; init; }
    public bool IsLikelyPublic { get; init; }
    public bool IsConfigured { get; init; }
    public IReadOnlyList<string> Images { get; init; } = [];
}
