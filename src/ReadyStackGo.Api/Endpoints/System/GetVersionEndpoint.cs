using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.System.GetVersion;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// GET /api/system/version
/// Get system version information and check for updates.
/// Accessible by all authenticated users.
/// </summary>
public class GetVersionEndpoint : Endpoint<GetVersionEndpoint.GetVersionRequest, GetVersionResponse>
{
    private readonly IMediator _mediator;

    public GetVersionEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/system/version");
        AllowAnonymous(); // Version info is public
    }

    public override async Task HandleAsync(GetVersionRequest req, CancellationToken ct)
    {
        var query = new GetVersionQuery(req.ForceCheck);
        Response = await _mediator.Send(query, ct);
    }

    public class GetVersionRequest
    {
        [QueryParam]
        public bool ForceCheck { get; set; }
    }
}
