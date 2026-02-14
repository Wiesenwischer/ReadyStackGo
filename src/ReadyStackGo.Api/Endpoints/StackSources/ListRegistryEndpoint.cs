using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.ListRegistrySources;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// GET /api/stack-sources/registry - List all entries from the curated source registry.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("StackSources", "Read")]
public class ListRegistryEndpoint : Endpoint<EmptyRequest, IEnumerable<RegistrySourceDto>>
{
    private readonly IMediator _mediator;

    public ListRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stack-sources/registry");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListRegistrySourcesQuery(), ct);

        Response = result.Sources.Select(s => new RegistrySourceDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            GitUrl = s.GitUrl,
            GitBranch = s.GitBranch,
            Category = s.Category,
            Tags = s.Tags,
            Featured = s.Featured,
            StackCount = s.StackCount,
            AlreadyAdded = s.AlreadyAdded
        });
    }
}

public class RegistrySourceDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string GitUrl { get; init; }
    public required string GitBranch { get; init; }
    public required string Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool Featured { get; init; }
    public int StackCount { get; init; }
    public bool AlreadyAdded { get; init; }
}
