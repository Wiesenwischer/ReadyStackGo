using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.DeleteEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

public class DeleteEnvironmentRequest
{
    /// <summary>
    /// Environment ID for RBAC scope check (from route).
    /// </summary>
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Deletes an environment. Requires Environments.Delete permission.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Environments", "Delete")]
public class DeleteEnvironmentEndpoint : Endpoint<DeleteEnvironmentRequest, DeleteEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public DeleteEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/environments/{id}");
        PreProcessor<RbacPreProcessor<DeleteEnvironmentRequest>>();
    }

    public override async Task HandleAsync(DeleteEnvironmentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("id")!;
        req.EnvironmentId = environmentId;
        var response = await _mediator.Send(new DeleteEnvironmentCommand(environmentId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to delete environment");
        }

        Response = response;
    }
}
