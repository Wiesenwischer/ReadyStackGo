using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.CreateStackSource;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// POST /api/stack-sources - Create a new stack source.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class CreateSourceEndpoint : Endpoint<CreateSourceApiRequest, CreateSourceResponse>
{
    private readonly IMediator _mediator;

    public CreateSourceEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/stack-sources");
        PreProcessor<RbacPreProcessor<CreateSourceApiRequest>>();
    }

    public override async Task HandleAsync(CreateSourceApiRequest req, CancellationToken ct)
    {
        var command = new CreateStackSourceCommand(new CreateStackSourceRequest
        {
            Id = req.Id,
            Name = req.Name,
            Type = req.Type,
            Path = req.Path,
            FilePattern = req.FilePattern,
            GitUrl = req.GitUrl,
            Branch = req.Branch
        });

        var result = await _mediator.Send(command, ct);

        Response = new CreateSourceResponse
        {
            Success = result.Success,
            Message = result.Message,
            SourceId = result.SourceId
        };

        if (result.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}

public class CreateSourceApiRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;  // "LocalDirectory" or "GitRepository"

    // For LocalDirectory
    public string? Path { get; set; }
    public string? FilePattern { get; set; }

    // For GitRepository
    public string? GitUrl { get; set; }
    public string? Branch { get; set; }
}

public class CreateSourceResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? SourceId { get; init; }
}
