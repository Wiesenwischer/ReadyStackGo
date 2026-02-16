using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Volumes.RemoveVolume;

namespace ReadyStackGo.Api.Endpoints.Volumes;

public class RemoveVolumeRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;

    [QueryParam]
    public bool Force { get; set; }
}

/// <summary>
/// Removes a Docker volume. Requires Deployments.Delete permission.
/// </summary>
[RequirePermission("Deployments", "Delete")]
public class RemoveVolumeEndpoint : Endpoint<RemoveVolumeRequest, EmptyResponse>
{
    private readonly IMediator _mediator;

    public RemoveVolumeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/volumes/{name}");
        PreProcessor<RbacPreProcessor<RemoveVolumeRequest>>();
        Description(b => b.WithTags("Volumes"));
    }

    public override async Task HandleAsync(RemoveVolumeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var name = Route<string>("name")!;

        var result = await _mediator.Send(
            new RemoveVolumeCommand(req.Environment, name, req.Force), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to remove volume");
        }
    }
}
