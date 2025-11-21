using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Stacks;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// Get a specific stack definition by ID
/// </summary>
public class GetStackDefinitionEndpoint : Endpoint<GetStackDefinitionRequest, StackDefinitionDetailDto>
{
    public IStackSourceService StackSourceService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stack-sources/stacks/{StackId}");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(GetStackDefinitionRequest req, CancellationToken ct)
    {
        var stack = await StackSourceService.GetStackAsync(req.StackId, ct);

        if (stack == null)
        {
            ThrowError("Stack not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = new StackDefinitionDetailDto
        {
            Id = stack.Id,
            SourceId = stack.SourceId,
            Name = stack.Name,
            Description = stack.Description,
            YamlContent = stack.YamlContent,
            Services = stack.Services,
            Variables = stack.Variables.Select(v => new StackVariableDto
            {
                Name = v.Name,
                DefaultValue = v.DefaultValue,
                IsRequired = v.IsRequired
            }).ToList(),
            FilePath = stack.FilePath,
            LastSyncedAt = stack.LastSyncedAt,
            Version = stack.Version
        };
    }
}

public class GetStackDefinitionRequest
{
    public string StackId { get; set; } = string.Empty;
}

public class StackDefinitionDetailDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string YamlContent { get; init; }
    public List<string> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
    public string? FilePath { get; init; }
    public DateTime LastSyncedAt { get; init; }
    public string? Version { get; init; }
}
