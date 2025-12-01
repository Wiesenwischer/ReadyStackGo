using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class RemoveDeploymentEndpoint : EndpointWithoutRequest<DeployComposeResponse>
{
    private readonly IMediator _mediator;

    public RemoveDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/deployments/{environmentId}/{stackName}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var stackName = Route<string>("stackName")!;

        var response = await _mediator.Send(new RemoveDeploymentCommand(environmentId, stackName), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to remove deployment");
        }

        Response = response;
    }
}
