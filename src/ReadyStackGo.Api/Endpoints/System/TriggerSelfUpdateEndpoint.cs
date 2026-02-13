using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.System.TriggerSelfUpdate;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// Request DTO for triggering a self-update.
/// </summary>
public class TriggerSelfUpdateRequest
{
    /// <summary>
    /// The target version to update to (e.g., "0.20.0").
    /// </summary>
    public string TargetVersion { get; set; } = string.Empty;
}

/// <summary>
/// POST /api/system/update
/// Triggers a self-update of the RSGO container to the specified version.
/// Accessible only by SystemAdmin.
/// </summary>
[RequirePermission("System", "Write")]
public class TriggerSelfUpdateEndpoint : Endpoint<TriggerSelfUpdateRequest, TriggerSelfUpdateResponse>
{
    private readonly IMediator _mediator;

    public TriggerSelfUpdateEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/system/update");
        Description(b => b.WithTags("System"));
        PreProcessor<RbacPreProcessor<TriggerSelfUpdateRequest>>();
    }

    public override async Task HandleAsync(TriggerSelfUpdateRequest req, CancellationToken ct)
    {
        var command = new TriggerSelfUpdateCommand(req.TargetVersion);
        Response = await _mediator.Send(command, ct);

        if (!Response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
