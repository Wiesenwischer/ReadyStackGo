using FastEndpoints;
using ReadyStackGo.Application.Deployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// POST /api/deployments/parse - Parse a Docker Compose file and detect variables
/// </summary>
public class ParseComposeEndpoint : Endpoint<ParseComposeRequest, ParseComposeResponse>
{
    public IDeploymentService DeploymentService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/deployments/parse");
    }

    public override async Task HandleAsync(ParseComposeRequest req, CancellationToken ct)
    {
        var response = await DeploymentService.ParseComposeAsync(req);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to parse compose file");
        }

        Response = response;
    }
}
