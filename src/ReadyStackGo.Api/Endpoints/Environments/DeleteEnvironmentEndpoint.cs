using FastEndpoints;
using ReadyStackGo.Application.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// DELETE /api/environments/{id} - Delete an environment
/// </summary>
public class DeleteEnvironmentEndpoint : EndpointWithoutRequest<DeleteEnvironmentResponse>
{
    public IEnvironmentService EnvironmentService { get; set; } = null!;

    public override void Configure()
    {
        Delete("/api/environments/{id}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("id");
        var response = await EnvironmentService.DeleteEnvironmentAsync(environmentId!);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to delete environment");
        }

        Response = response;
    }
}
