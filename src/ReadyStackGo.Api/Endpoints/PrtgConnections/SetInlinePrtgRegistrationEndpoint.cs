using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.PrtgConnections;

namespace ReadyStackGo.Api.Endpoints.PrtgConnections;

/// <summary>
/// PUT /api/deployments/{Id}/prtg-inline — sets ad-hoc per-deployment PRTG
/// credentials (Variant 2). Body with null/empty URL clears the inline
/// registration. Saved-connection (Variant 3) link is cleared as a side
/// effect when an inline target is set — only one PRTG target is active.
/// </summary>
[RequirePermission("Deployments", "Manage")]
public class SetInlinePrtgRegistrationEndpoint : Endpoint<SetInlinePrtgRegistrationRequest>
{
    private readonly IMediator _mediator;

    public SetInlinePrtgRegistrationEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Put("/api/deployments/{Id}/prtg-inline");

    public override async Task HandleAsync(SetInlinePrtgRegistrationRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SetInlinePrtgRegistrationCommand(req.Id, req.Url, req.ApiToken, req.TemplateDeviceId, req.VerifyTls),
            ct);
        if (!result.Success)
        {
            ThrowError(result.Error ?? "Failed to set inline PRTG registration.", StatusCodes.Status400BadRequest);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}

public class SetInlinePrtgRegistrationRequest
{
    public Guid Id { get; set; }                    // ProductDeploymentId — bound from route
    public string? Url { get; set; }                // null/empty = clear
    public string? ApiToken { get; set; }
    public int? TemplateDeviceId { get; set; }
    public bool VerifyTls { get; set; } = true;
}
