using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.ListStackSources;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// GET /api/stack-sources - List all configured stack sources.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("StackSources", "Read")]
public class ListSourcesEndpoint : Endpoint<EmptyRequest, IEnumerable<StackSourceDto>>
{
    private readonly IMediator _mediator;

    public ListSourcesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stack-sources");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListStackSourcesQuery(), ct);

        Response = result.Sources.Select(s => new StackSourceDto
        {
            Id = s.Id,
            Name = s.Name,
            Type = s.Type,
            Enabled = s.Enabled,
            LastSyncedAt = s.LastSyncedAt,
            Details = s.Details
        });
    }
}

public class StackSourceDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Enabled { get; init; }
    public DateTime? LastSyncedAt { get; init; }
    public Dictionary<string, string> Details { get; init; } = new();
}
