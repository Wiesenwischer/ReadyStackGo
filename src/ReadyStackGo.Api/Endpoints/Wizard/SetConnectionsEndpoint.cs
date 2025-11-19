using FastEndpoints;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/connections - Step 3: Set connections (Simple mode)
/// </summary>
public class SetConnectionsEndpoint : Endpoint<SetConnectionsRequest, SetConnectionsResponse>
{
    public IWizardService WizardService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/wizard/connections");
        AllowAnonymous(); // Wizard endpoints are accessible before auth setup
    }

    public override async Task HandleAsync(SetConnectionsRequest req, CancellationToken ct)
    {
        var result = await WizardService.SetConnectionsAsync(req);

        if (!result.Success)
        {
            ThrowError(result.Message ?? "Failed to set connections");
        }

        Response = result;
    }
}
