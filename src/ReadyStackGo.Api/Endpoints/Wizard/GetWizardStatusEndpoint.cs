using FastEndpoints;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// GET /api/wizard/status - Get current wizard state
/// </summary>
public class GetWizardStatusEndpoint : EndpointWithoutRequest<WizardStatusResponse>
{
    public IWizardService WizardService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/wizard/status");
        AllowAnonymous(); // Wizard endpoints are accessible before auth setup
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = await WizardService.GetWizardStatusAsync();
        Response = status;
    }
}
