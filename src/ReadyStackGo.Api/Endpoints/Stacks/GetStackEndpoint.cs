using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Stacks.GetStack;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/stacks/{id} - Get a specific stack by ID.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Stacks", "Read")]
public class GetStackEndpoint : Endpoint<GetStackRequest, StackDetailDto>
{
    private readonly IMediator _mediator;

    public GetStackEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stacks/{Id}");
        PreProcessor<RbacPreProcessor<GetStackRequest>>();
    }

    public override async Task HandleAsync(GetStackRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStackQuery(req.Id), ct);

        if (result == null)
        {
            ThrowError("Stack not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = new StackDetailDto
        {
            Id = result.Id,
            SourceId = result.SourceId,
            SourceName = result.SourceName,
            Name = result.Name,
            Description = result.Description,
            YamlContent = result.YamlContent,
            Services = result.Services,
            Variables = result.Variables.Select(v => new StackVariableDto
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
            FilePath = result.FilePath,
            AdditionalFiles = result.AdditionalFiles,
            LastSyncedAt = result.LastSyncedAt,
            Version = result.Version
        };
    }
}

public class GetStackRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Detailed stack DTO including YAML content
/// </summary>
public class StackDetailDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string YamlContent { get; init; }
    public List<string> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
    public string? FilePath { get; init; }
    public List<string> AdditionalFiles { get; init; } = new();
    public DateTime LastSyncedAt { get; init; }
    public string? Version { get; init; }
}
