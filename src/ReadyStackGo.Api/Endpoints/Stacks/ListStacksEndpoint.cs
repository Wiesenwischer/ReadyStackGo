using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/stacks - List all stacks from all sources.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Stacks", "Read")]
public class ListStacksEndpoint : Endpoint<EmptyRequest, IEnumerable<StackDto>>
{
    private readonly IMediator _mediator;

    public ListStacksEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stacks");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
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
                IsRequired = v.IsRequired,
                Type = v.Type.ToString(),
                Label = v.Label,
                Description = v.Description,
                Placeholder = v.Placeholder,
                Group = v.Group,
                Order = v.Order,
                Pattern = v.Pattern,
                PatternError = v.PatternError,
                Min = v.Min,
                Max = v.Max,
                Options = v.Options?.Select(o => new SelectOptionDto
                {
                    Value = o.Value,
                    Label = o.Label,
                    Description = o.Description
                }).ToList()
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
/// Stack variable DTO with full type information
/// </summary>
public class StackVariableDto
{
    public required string Name { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsRequired { get; init; }
    public string Type { get; init; } = "String";
    public string? Label { get; init; }
    public string? Description { get; init; }
    public string? Placeholder { get; init; }
    public string? Group { get; init; }
    public int? Order { get; init; }
    public string? Pattern { get; init; }
    public string? PatternError { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public List<SelectOptionDto>? Options { get; init; }
}

public class SelectOptionDto
{
    public required string Value { get; init; }
    public string? Label { get; init; }
    public string? Description { get; init; }
}
