using FastEndpoints;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/admin - Step 1: Create admin user
/// </summary>
public class CreateAdminEndpoint : Endpoint<CreateAdminRequest, CreateAdminResponse>
{
    public IWizardService WizardService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/wizard/admin");
        AllowAnonymous(); // Wizard endpoints are accessible before auth setup
    }

    public override async Task HandleAsync(CreateAdminRequest req, CancellationToken ct)
    {
        var result = await WizardService.CreateAdminAsync(req);

        if (!result.Success)
        {
            ThrowError(result.Message ?? "Failed to create admin user");
        }

        Response = result;
    }
}
