using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.GetEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

public class GetEnvironmentEndpoint : EndpointWithoutRequest<EnvironmentResponse>
{
    private readonly IMediator _mediator;

    public GetEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{id}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("id")!;
        var response = await _mediator.Send(new GetEnvironmentQuery(environmentId), ct);

        if (response == null)
        {
            ThrowError("Environment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}
