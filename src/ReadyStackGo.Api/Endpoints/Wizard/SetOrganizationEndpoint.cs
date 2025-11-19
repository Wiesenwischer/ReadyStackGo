using FastEndpoints;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/organization - Step 2: Set organization
/// </summary>
public class SetOrganizationEndpoint : Endpoint<SetOrganizationRequest, SetOrganizationResponse>
{
    public IWizardService WizardService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/wizard/organization");
        AllowAnonymous(); // Wizard endpoints are accessible before auth setup
    }

    public override async Task HandleAsync(SetOrganizationRequest req, CancellationToken ct)
    {
        var result = await WizardService.SetOrganizationAsync(req);

        if (!result.Success)
        {
            ThrowError(result.Message ?? "Failed to set organization");
        }

        Response = result;
    }
}
