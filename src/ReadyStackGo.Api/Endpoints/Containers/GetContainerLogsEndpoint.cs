using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.GetContainerLogs;

namespace ReadyStackGo.API.Endpoints.Containers;

public class GetContainerLogsRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    [QueryParam]
    public int? Tail { get; set; }

    /// <summary>
    /// Environment ID for RBAC scope check (alias for Environment).
    /// </summary>
    public string? EnvironmentId => Environment;
}

/// <summary>
/// Gets container logs. Requires Deployments.Read permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetContainerLogsEndpoint : Endpoint<GetContainerLogsRequest>
{
    private readonly IMediator _mediator;

    public GetContainerLogsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/containers/{id}/logs");
        PreProcessor<RbacPreProcessor<GetContainerLogsRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(GetContainerLogsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var id = Route<string>("id")!;

        var result = await _mediator.Send(
            new GetContainerLogsQuery(req.Environment, id, req.Tail), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to get container logs");
        }

        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.WriteAsync(result.Logs, ct);
    }
}
