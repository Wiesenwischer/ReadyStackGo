using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.GetStackSource;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// GET /api/stack-sources/{id} - Get a specific stack source.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("StackSources", "Read")]
public class GetSourceEndpoint : Endpoint<GetSourceRequest, StackSourceDetailDto>
{
    private readonly IMediator _mediator;

    public GetSourceEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stack-sources/{Id}");
        PreProcessor<RbacPreProcessor<GetSourceRequest>>();
    }

    public override async Task HandleAsync(GetSourceRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStackSourceQuery(req.Id), ct);

        if (result == null)
        {
            ThrowError("Stack source not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = new StackSourceDetailDto
        {
            Id = result.Id,
            Name = result.Name,
            Type = result.Type,
            Enabled = result.Enabled,
            LastSyncedAt = result.LastSyncedAt,
            CreatedAt = result.CreatedAt,
            Path = result.Path,
            FilePattern = result.FilePattern,
            GitUrl = result.GitUrl,
            GitBranch = result.GitBranch,
            GitUsername = result.GitUsername,
            HasGitPassword = result.HasGitPassword
        };
    }
}

public class GetSourceRequest
{
    public string Id { get; set; } = string.Empty;
}

public class StackSourceDetailDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Enabled { get; init; }
    public DateTime? LastSyncedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Path { get; init; }
    public string? FilePattern { get; init; }
    public string? GitUrl { get; init; }
    public string? GitBranch { get; init; }
    public string? GitUsername { get; init; }
    public bool HasGitPassword { get; init; }
}
