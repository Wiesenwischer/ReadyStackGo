using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Wizard.SetConnections;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/connections - Step 3: Set connections (Simple mode)
/// </summary>
public class SetConnectionsEndpoint : Endpoint<SetConnectionsRequest, SetConnectionsResponse>
{
    private readonly IMediator _mediator;

    public SetConnectionsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/wizard/connections");
        AllowAnonymous(); // Wizard endpoints are accessible before auth setup
    }

    public override async Task HandleAsync(SetConnectionsRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SetConnectionsCommand(req.Transport, req.Persistence, req.EventStore),
            ct);

        if (!result.Success)
        {
            ThrowError(result.Message ?? "Failed to set connections");
        }

        Response = new SetConnectionsResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }
}
