using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.ListDeployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class ListDeploymentsEndpoint : EndpointWithoutRequest<ListDeploymentsResponse>
{
    private readonly IMediator _mediator;

    public ListDeploymentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/deployments/{environmentId}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        Response = await _mediator.Send(new ListDeploymentsQuery(environmentId), ct);
    }
}
