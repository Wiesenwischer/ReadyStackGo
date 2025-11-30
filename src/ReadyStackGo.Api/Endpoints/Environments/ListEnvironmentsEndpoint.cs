using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.ListEnvironments;

namespace ReadyStackGo.API.Endpoints.Environments;

public class ListEnvironmentsEndpoint : EndpointWithoutRequest<ListEnvironmentsResponse>
{
    private readonly IMediator _mediator;

    public ListEnvironmentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Response = await _mediator.Send(new ListEnvironmentsQuery(), ct);
    }
}
