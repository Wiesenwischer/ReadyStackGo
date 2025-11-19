using FastEndpoints;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/install - Step 4: Install stack from manifest
/// </summary>
public class InstallStackEndpoint : Endpoint<InstallStackRequest, InstallStackResponse>
{
    public IWizardService WizardService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/wizard/install");
        AllowAnonymous(); // Wizard endpoints are accessible before auth setup
    }

    public override async Task HandleAsync(InstallStackRequest req, CancellationToken ct)
    {
        var result = await WizardService.InstallStackAsync(req);

        if (!result.Success)
        {
            var errorMessage = result.Errors.Count > 0
                ? string.Join(", ", result.Errors)
                : "Failed to install stack";
            ThrowError(errorMessage);
        }

        Response = result;
    }
}
