using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// GET /api/environments/{id} - Get a specific environment
/// </summary>
public class GetEnvironmentEndpoint : EndpointWithoutRequest<EnvironmentResponse>
{
    public IEnvironmentService EnvironmentService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/environments/{id}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("id");
        var response = await EnvironmentService.GetEnvironmentAsync(environmentId!);

        if (response == null)
        {
            ThrowError("Environment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}
