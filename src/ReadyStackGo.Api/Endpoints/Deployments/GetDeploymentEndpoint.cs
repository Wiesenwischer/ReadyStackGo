using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.GetDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class GetDeploymentEndpoint : EndpointWithoutRequest<GetDeploymentResponse>
{
    private readonly IMediator _mediator;

    public GetDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/deployments/{environmentId}/{stackName}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var stackName = Route<string>("stackName")!;

        var response = await _mediator.Send(new GetDeploymentQuery(environmentId, stackName), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Deployment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}
