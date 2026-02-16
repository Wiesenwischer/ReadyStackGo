using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Volumes;
using ReadyStackGo.Application.UseCases.Volumes.GetVolume;

namespace ReadyStackGo.Api.Endpoints.Volumes;

public class GetVolumeRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;
}

/// <summary>
/// Gets details for a single Docker volume. Requires Deployments.Read permission.
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetVolumeEndpoint : Endpoint<GetVolumeRequest, VolumeDto>
{
    private readonly IMediator _mediator;

    public GetVolumeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/volumes/{name}");
        PreProcessor<RbacPreProcessor<GetVolumeRequest>>();
        Description(b => b.WithTags("Volumes"));
    }

    public override async Task HandleAsync(GetVolumeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var name = Route<string>("name")!;

        var result = await _mediator.Send(new GetVolumeQuery(req.Environment, name), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to get volume");
        }

        Response = result.Volume!;
    }
}
