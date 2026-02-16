using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Volumes;
using ReadyStackGo.Application.UseCases.Volumes.ListVolumes;

namespace ReadyStackGo.Api.Endpoints.Volumes;

public class ListVolumesRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;
}

/// <summary>
/// Lists Docker volumes for an environment. Requires Deployments.Read permission.
/// </summary>
[RequirePermission("Deployments", "Read")]
public class ListVolumesEndpoint : Endpoint<ListVolumesRequest, IReadOnlyList<VolumeDto>>
{
    private readonly IMediator _mediator;

    public ListVolumesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/volumes");
        PreProcessor<RbacPreProcessor<ListVolumesRequest>>();
        Description(b => b.WithTags("Volumes"));
    }

    public override async Task HandleAsync(ListVolumesRequest req, CancellationToken ct)
    {
        var environment = Query<string>("environment", false);
        if (string.IsNullOrWhiteSpace(environment))
        {
            ThrowError("Environment is required");
        }

        var result = await _mediator.Send(new ListVolumesQuery(environment), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to list volumes");
        }

        Response = result.Volumes;
    }
}
