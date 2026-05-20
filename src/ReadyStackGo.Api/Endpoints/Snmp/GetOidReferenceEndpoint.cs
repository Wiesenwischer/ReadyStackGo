using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Api.Endpoints.Snmp;

/// <summary>
/// GET /api/snmp/oid-reference — admin-facing endpoint that returns the live
/// OID tree (system scalars + environment/product/stack/service tree with
/// concrete numeric OIDs) so admins can configure their monitoring tools
/// without running snmpwalk discovery.
/// </summary>
[RequirePermission("Settings", "Read")]
public class GetOidReferenceEndpoint : EndpointWithoutRequest<OidReferenceResult>
{
    private readonly IMediator _mediator;

    public GetOidReferenceEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/snmp/oid-reference");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Response = await _mediator.Send(new GetOidReferenceQuery(), ct);
    }
}
