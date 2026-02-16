using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.GetContainerContext;

namespace ReadyStackGo.API.Endpoints.Containers;

public class GetContainerContextRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    /// <summary>
    /// Environment ID for RBAC scope check (alias for Environment).
    /// </summary>
    public string? EnvironmentId => Environment;
}

/// <summary>
/// Returns stack/product context for containers. Requires Deployments.Read permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetContainerContextEndpoint : Endpoint<GetContainerContextRequest, GetContainerContextResult>
{
    private readonly IMediator _mediator;

    public GetContainerContextEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/containers/context");
        PreProcessor<RbacPreProcessor<GetContainerContextRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(GetContainerContextRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var result = await _mediator.Send(new GetContainerContextQuery(req.Environment), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to get container context");
        }

        Response = result;
    }
}
