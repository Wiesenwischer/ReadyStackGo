using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.System.GetTlsConfig;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// GET /api/system/tls
/// Get current TLS configuration and certificate information.
/// Accessible only by SystemAdmin.
/// </summary>
[RequirePermission("System", "Read")]
public class GetTlsConfigEndpoint : EndpointWithoutRequest<GetTlsConfigResponse>
{
    private readonly IMediator _mediator;

    public GetTlsConfigEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/system/tls");
        Description(b => b.WithTags("System"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetTlsConfigQuery();
        Response = await _mediator.Send(query, ct);
    }
}
