using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.PrtgConnections;

namespace ReadyStackGo.Api.Endpoints.PrtgConnections;

/// <summary>
/// PUT /api/deployments/{Id}/prtg-connection — links (or unlinks if body
/// is null/empty) a ProductDeployment to a reusable PRTG connection. The
/// next lifecycle event (Completed/Removed/Superseded) on the deployment
/// then registers/deregisters the PRTG device.
/// </summary>
[RequirePermission("Deployments", "Manage")]
public class LinkPrtgConnectionEndpoint : Endpoint<LinkPrtgConnectionRequest>
{
    private readonly IMediator _mediator;

    public LinkPrtgConnectionEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Put("/api/deployments/{Id}/prtg-connection");

    public override async Task HandleAsync(LinkPrtgConnectionRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new LinkPrtgConnectionCommand(req.Id, req.PrtgConnectionId), ct);
        if (!result.Success)
        {
            ThrowError(result.Error ?? "Failed to link PRTG connection.", StatusCodes.Status400BadRequest);
            return;
        }
        // NoContent — Send.OkAsync(ct) binds `ct` positionally as TResponse and
        // tries to serialize the CancellationToken (throws on WaitHandle.Handle
        // IntPtr). This endpoint has no response body, so 204 is the right code.
        await Send.NoContentAsync(ct);
    }
}

public class LinkPrtgConnectionRequest
{
    public Guid Id { get; set; }                    // ProductDeploymentId — bound from route
    public Guid? PrtgConnectionId { get; set; }     // null = unlink
}
