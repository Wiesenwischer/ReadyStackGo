using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.PrtgConnections;

namespace ReadyStackGo.Api.Endpoints.PrtgConnections;

/// <summary>
/// CRUD endpoints for the <c>PrtgConnection</c> aggregate (V3 of the PRTG
/// integration). Connections are reusable, org-scoped and used by the
/// ProductDeployment lifecycle to register/deregister devices in PRTG.
/// </summary>
[RequirePermission("Settings", "Read")]
public class ListPrtgConnectionsEndpoint : EndpointWithoutRequest<IReadOnlyList<PrtgConnectionDto>>
{
    private readonly IMediator _mediator;
    public ListPrtgConnectionsEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Get("/api/prtg-connections");

    public override async Task HandleAsync(CancellationToken ct)
        => Response = await _mediator.Send(new ListPrtgConnectionsQuery(), ct);
}

[RequirePermission("Settings", "Read")]
public class GetPrtgConnectionEndpoint : Endpoint<GetPrtgConnectionRequest, PrtgConnectionDto>
{
    private readonly IMediator _mediator;
    public GetPrtgConnectionEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Get("/api/prtg-connections/{Id}");

    public override async Task HandleAsync(GetPrtgConnectionRequest req, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetPrtgConnectionQuery(req.Id), ct);
        if (dto is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        Response = dto;
    }
}

public class GetPrtgConnectionRequest
{
    public Guid Id { get; set; }
}

[RequirePermission("Settings", "Manage")]
public class CreatePrtgConnectionEndpoint : Endpoint<CreatePrtgConnectionRequest, PrtgConnectionResponse>
{
    private readonly IMediator _mediator;
    public CreatePrtgConnectionEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Post("/api/prtg-connections");

    public override async Task HandleAsync(CreatePrtgConnectionRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePrtgConnectionCommand(req), ct);
        if (!result.Success)
        {
            ThrowError(result.Error ?? "Failed to create PRTG connection.", StatusCodes.Status400BadRequest);
            return;
        }
        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        Response = result;
    }
}

[RequirePermission("Settings", "Manage")]
public class UpdatePrtgConnectionEndpoint : Endpoint<UpdatePrtgConnectionRouteRequest, PrtgConnectionResponse>
{
    private readonly IMediator _mediator;
    public UpdatePrtgConnectionEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Put("/api/prtg-connections/{Id}");

    public override async Task HandleAsync(UpdatePrtgConnectionRouteRequest req, CancellationToken ct)
    {
        var body = new UpdatePrtgConnectionRequest(req.Name, req.Url, req.ApiToken, req.TemplateDeviceId, req.VerifyTls);
        var result = await _mediator.Send(new UpdatePrtgConnectionCommand(req.Id, body), ct);
        if (!result.Success)
        {
            ThrowError(result.Error ?? "Failed to update PRTG connection.", StatusCodes.Status400BadRequest);
            return;
        }
        Response = result;
    }
}

/// <summary>Route + body combined; the route binds {Id}, the rest comes from JSON.</summary>
public class UpdatePrtgConnectionRouteRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ApiToken { get; set; }
    public int? TemplateDeviceId { get; set; }
    public bool VerifyTls { get; set; } = true;
}

[RequirePermission("Settings", "Manage")]
public class DeletePrtgConnectionEndpoint : Endpoint<DeletePrtgConnectionRequest>
{
    private readonly IMediator _mediator;
    public DeletePrtgConnectionEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Delete("/api/prtg-connections/{Id}");

    public override async Task HandleAsync(DeletePrtgConnectionRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeletePrtgConnectionCommand(req.Id), ct);
        if (!result.Success)
        {
            ThrowError(result.Error ?? "Failed to delete PRTG connection.", StatusCodes.Status404NotFound);
            return;
        }
        await Send.OkAsync(ct);
    }
}

public class DeletePrtgConnectionRequest
{
    public Guid Id { get; set; }
}
