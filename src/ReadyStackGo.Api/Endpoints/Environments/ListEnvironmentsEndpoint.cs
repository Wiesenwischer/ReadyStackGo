using FastEndpoints;
using ReadyStackGo.Application.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// GET /api/environments - List all environments
/// </summary>
public class ListEnvironmentsEndpoint : EndpointWithoutRequest<ListEnvironmentsResponse>
{
    public IEnvironmentService EnvironmentService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/environments");
        // Requires authentication (default)
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await EnvironmentService.GetEnvironmentsAsync();
        Response = response;
    }
}
