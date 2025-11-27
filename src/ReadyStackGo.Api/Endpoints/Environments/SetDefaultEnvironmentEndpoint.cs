using FastEndpoints;
using ReadyStackGo.Application.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// POST /api/environments/{id}/default - Set an environment as default
/// </summary>
public class SetDefaultEnvironmentEndpoint : EndpointWithoutRequest<SetDefaultEnvironmentResponse>
{
    public IEnvironmentService EnvironmentService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/environments/{id}/default");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("id");
        var response = await EnvironmentService.SetDefaultEnvironmentAsync(environmentId!);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to set default environment");
        }

        Response = response;
    }
}
