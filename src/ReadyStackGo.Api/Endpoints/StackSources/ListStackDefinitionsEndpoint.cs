using FastEndpoints;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Domain.Stacks;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// Get all stack definitions from the cache
/// </summary>
public class ListStackDefinitionsEndpoint : EndpointWithoutRequest<IEnumerable<StackDefinitionDto>>
{
    public IStackSourceService StackSourceService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stack-sources/stacks");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stacks = await StackSourceService.GetStacksAsync(ct);
        Response = stacks.Select(s => new StackDefinitionDto
        {
            Id = s.Id,
            SourceId = s.SourceId,
            Name = s.Name,
            Description = s.Description,
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

public class StackDefinitionDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<string> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
    public DateTime LastSyncedAt { get; init; }
    public string? Version { get; init; }
}

public class StackVariableDto
{
    public required string Name { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsRequired { get; init; }
}
