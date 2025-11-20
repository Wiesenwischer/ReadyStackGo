using FastEndpoints;
using ReadyStackGo.Application.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// PUT /api/environments/{id} - Update an environment
/// </summary>
public class UpdateEnvironmentEndpoint : Endpoint<UpdateEnvironmentRequest, UpdateEnvironmentResponse>
{
    public IEnvironmentService EnvironmentService { get; set; } = null!;

    public override void Configure()
    {
        Put("/api/environments/{id}");
    }

    public override async Task HandleAsync(UpdateEnvironmentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("id");
        var response = await EnvironmentService.UpdateEnvironmentAsync(environmentId!, req);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to update environment");
        }

        Response = response;
    }
}
