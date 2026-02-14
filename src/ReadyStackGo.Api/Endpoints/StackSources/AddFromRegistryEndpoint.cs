using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.AddFromRegistry;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// POST /api/stack-sources/from-registry - Add a stack source from the curated registry.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class AddFromRegistryEndpoint : Endpoint<AddFromRegistryApiRequest, AddFromRegistryApiResponse>
{
    private readonly IMediator _mediator;

    public AddFromRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/stack-sources/from-registry");
        PreProcessor<RbacPreProcessor<AddFromRegistryApiRequest>>();
    }

    public override async Task HandleAsync(AddFromRegistryApiRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddFromRegistryCommand(req.RegistrySourceId), ct);

        Response = new AddFromRegistryApiResponse
        {
            Success = result.Success,
            Message = result.Message,
            SourceId = result.SourceId
        };

        HttpContext.Response.StatusCode = result.Success
            ? StatusCodes.Status201Created
            : StatusCodes.Status400BadRequest;
    }
}

public class AddFromRegistryApiRequest
{
    public string RegistrySourceId { get; set; } = string.Empty;
}

public class AddFromRegistryApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? SourceId { get; init; }
}
