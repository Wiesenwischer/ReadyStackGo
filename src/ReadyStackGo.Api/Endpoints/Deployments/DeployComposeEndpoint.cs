using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployCompose;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class DeployComposeEndpoint : Endpoint<DeployComposeRequest, DeployComposeResponse>
{
    private readonly IMediator _mediator;

    public DeployComposeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/deployments/{environmentId}");
    }

    public override async Task HandleAsync(DeployComposeRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;

        var response = await _mediator.Send(
            new DeployComposeCommand(environmentId, req.StackName, req.YamlContent, req.Variables), ct);

        if (!response.Success && response.Message?.Contains("not found") == true)
        {
            ThrowError(response.Message);
        }

        Response = response;
    }
}
