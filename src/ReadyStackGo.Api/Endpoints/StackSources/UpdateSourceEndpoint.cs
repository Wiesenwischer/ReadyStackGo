using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.UpdateStackSource;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// PUT /api/stack-sources/{id} - Update a stack source.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class UpdateSourceEndpoint : Endpoint<UpdateSourceApiRequest, UpdateSourceResponse>
{
    private readonly IMediator _mediator;

    public UpdateSourceEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/stack-sources/{Id}");
        PreProcessor<RbacPreProcessor<UpdateSourceApiRequest>>();
    }

    public override async Task HandleAsync(UpdateSourceApiRequest req, CancellationToken ct)
    {
        var command = new UpdateStackSourceCommand(req.Id, new UpdateStackSourceRequest
        {
            Name = req.Name,
            Enabled = req.Enabled
        });

        var result = await _mediator.Send(command, ct);

        Response = new UpdateSourceResponse
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

public class UpdateSourceApiRequest
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
}

public class UpdateSourceResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}
