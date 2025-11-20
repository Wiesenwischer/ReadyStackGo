using FastEndpoints;
using ReadyStackGo.Application.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// POST /api/environments - Create a new environment
/// </summary>
public class CreateEnvironmentEndpoint : Endpoint<CreateEnvironmentRequest, CreateEnvironmentResponse>
{
    public IEnvironmentService EnvironmentService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/environments");
    }

    public override async Task HandleAsync(CreateEnvironmentRequest req, CancellationToken ct)
    {
        var response = await EnvironmentService.CreateEnvironmentAsync(req);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to create environment");
        }

        Response = response;
    }
}
