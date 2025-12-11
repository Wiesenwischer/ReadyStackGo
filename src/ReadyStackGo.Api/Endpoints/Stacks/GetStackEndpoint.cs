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
            Services = result.Services.Select(s => new ServiceDto
            {
                Name = s.Name,
                Image = s.Image,
                ContainerName = s.ContainerName,
                Ports = s.Ports,
                Environment = s.Environment,
                Volumes = s.Volumes,
                Networks = s.Networks,
                DependsOn = s.DependsOn
            }).ToList(),
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
            Volumes = result.Volumes.Select(v => new VolumeDto
            {
                Name = v.Name,
                Driver = v.Driver,
                External = v.External
            }).ToList(),
            Networks = result.Networks.Select(n => new NetworkDto
            {
                Name = n.Name,
                Driver = n.Driver,
                External = n.External
            }).ToList(),
            FilePath = result.FilePath,
            LastSyncedAt = result.LastSyncedAt,
            Version = result.Version,
            ProductId = result.ProductId
        };
    }
}

public class GetStackRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Detailed stack DTO with structured service data.
/// v0.12: Replaced YamlContent with structured Services, Volumes, Networks.
/// </summary>
public class StackDetailDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<ServiceDto> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
    public List<VolumeDto> Volumes { get; init; } = new();
    public List<NetworkDto> Networks { get; init; } = new();
    public string? FilePath { get; init; }
    public DateTime LastSyncedAt { get; init; }
    public string? Version { get; init; }
    /// <summary>
    /// Product ID for navigation back to catalog (format: sourceId:productName).
    /// </summary>
    public required string ProductId { get; init; }
}

/// <summary>
/// Service definition DTO.
/// </summary>
public class ServiceDto
{
    public required string Name { get; init; }
    public required string Image { get; init; }
    public string? ContainerName { get; init; }
    public List<string> Ports { get; init; } = new();
    public Dictionary<string, string> Environment { get; init; } = new();
    public List<string> Volumes { get; init; } = new();
    public List<string> Networks { get; init; } = new();
    public List<string> DependsOn { get; init; } = new();
}

/// <summary>
/// Named volume definition DTO.
/// </summary>
public class VolumeDto
{
    public required string Name { get; init; }
    public string? Driver { get; init; }
    public bool External { get; init; }
}

/// <summary>
/// Network definition DTO.
/// </summary>
public class NetworkDto
{
    public required string Name { get; init; }
    public string? Driver { get; init; }
    public bool External { get; init; }
}
