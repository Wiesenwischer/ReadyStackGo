using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.ListEnvironments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// GET /api/environments - List all environments.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Environments", "Read")]
public class ListEnvironmentsEndpoint : Endpoint<EmptyRequest, ListEnvironmentsResponse>
{
    private readonly IMediator _mediator;

    public ListEnvironmentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        Response = await _mediator.Send(new ListEnvironmentsQuery(), ct);
    }
}
