using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/stacks - List all stacks from all sources
/// </summary>
public class ListStacksEndpoint : EndpointWithoutRequest<IEnumerable<StackDto>>
{
    private readonly IMediator _mediator;

    public ListStacksEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stacks");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListStacksQuery(), ct);

        Response = result.Stacks.Select(s => new StackDto
        {
            Id = s.Id,
            SourceId = s.SourceId,
            SourceName = s.SourceName,
            Name = s.Name,
            Description = s.Description,
            RelativePath = s.RelativePath,
            Services = s.Services,
            Variables = s.Variables.Select(v => new StackVariableDto
            {
                Name = v.Name,
                DefaultValue = v.DefaultValue,
                IsRequired = v.IsRequired
            }).ToList(),
            LastSyncedAt = s.LastSyncedAt,
            Version = s.Version
        });
    }
}

/// <summary>
/// Stack DTO for API responses
/// </summary>
public class StackDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? RelativePath { get; init; }
    public List<string> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
    public DateTime LastSyncedAt { get; init; }
    public string? Version { get; init; }
}

/// <summary>
/// Stack variable DTO
/// </summary>
public class StackVariableDto
{
    public required string Name { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsRequired { get; init; }
}
