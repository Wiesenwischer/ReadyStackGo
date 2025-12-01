using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// Creates a new environment. Requires Environments.Create permission.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Environments", "Create")]
public class CreateEnvironmentEndpoint : Endpoint<CreateEnvironmentRequest, CreateEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public CreateEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments");
        PreProcessor<RbacPreProcessor<CreateEnvironmentRequest>>();
    }

    public override async Task HandleAsync(CreateEnvironmentRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CreateEnvironmentCommand(req.Name, req.SocketPath), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to create environment");
        }

        Response = response;
    }
}
