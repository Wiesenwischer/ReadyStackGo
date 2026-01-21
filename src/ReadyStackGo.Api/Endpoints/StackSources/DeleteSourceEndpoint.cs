using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.DeleteStackSource;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// DELETE /api/stack-sources/{id} - Delete a stack source.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class DeleteSourceEndpoint : Endpoint<DeleteSourceRequest, DeleteSourceResponse>
{
    private readonly IMediator _mediator;

    public DeleteSourceEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/stack-sources/{Id}");
        PreProcessor<RbacPreProcessor<DeleteSourceRequest>>();
    }

    public override async Task HandleAsync(DeleteSourceRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteStackSourceCommand(req.Id), ct);

        Response = new DeleteSourceResponse
        {
            Success = result.Success,
            Message = result.Message
        };

        if (!result.Success && result.Message?.Contains("not found") == true)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        }
        else if (!result.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}

public class DeleteSourceRequest
{
    public string Id { get; set; } = string.Empty;
}

public class DeleteSourceResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}
