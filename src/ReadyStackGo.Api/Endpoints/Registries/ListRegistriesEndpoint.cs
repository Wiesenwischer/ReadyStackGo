using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

/// <summary>
/// GET /api/registries - List all registries for the organization.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator.
/// </summary>
[RequirePermission("Registries", "Read")]
public class ListRegistriesEndpoint : Endpoint<EmptyRequest, ListRegistriesResponse>
{
    private readonly IMediator _mediator;

    public ListRegistriesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/registries");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        // Organization is resolved in the handler
        Response = await _mediator.Send(new ListRegistriesQuery(), ct);
    }
}
