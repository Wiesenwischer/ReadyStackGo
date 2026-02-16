using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Volumes;
using ReadyStackGo.Application.UseCases.Volumes.CreateVolume;

namespace ReadyStackGo.Api.Endpoints.Volumes;

public class CreateVolumeRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;

    public string Name { get; set; } = null!;
    public string? Driver { get; set; }
    public IDictionary<string, string>? Labels { get; set; }
}

/// <summary>
/// Creates a new Docker volume. Requires Deployments.Create permission.
/// </summary>
[RequirePermission("Deployments", "Create")]
public class CreateVolumeEndpoint : Endpoint<CreateVolumeRequest, VolumeDto>
{
    private readonly IMediator _mediator;

    public CreateVolumeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/volumes");
        PreProcessor<RbacPreProcessor<CreateVolumeRequest>>();
        Description(b => b.WithTags("Volumes"));
    }

    public override async Task HandleAsync(CreateVolumeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var result = await _mediator.Send(
            new CreateVolumeCommand(req.Environment, req.Name, req.Driver, req.Labels), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to create volume");
        }

        Response = result.Volume!;
    }
}
