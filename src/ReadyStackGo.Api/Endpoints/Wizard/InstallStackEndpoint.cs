using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Wizard.CompleteWizard;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/install - Step 3: Complete wizard setup
/// </summary>
public class InstallStackEndpoint : Endpoint<InstallStackRequest, InstallStackResponse>
{
    private readonly IMediator _mediator;
    private readonly IWizardTimeoutService _wizardTimeoutService;

    public InstallStackEndpoint(IMediator mediator, IWizardTimeoutService wizardTimeoutService)
    {
        _mediator = mediator;
        _wizardTimeoutService = wizardTimeoutService;
    }

    public override void Configure()
    {
        Post("/api/wizard/install");
        AllowAnonymous();
        PreProcessor<WizardTimeoutPreProcessor<InstallStackRequest>>();
    }

    public override async Task HandleAsync(InstallStackRequest req, CancellationToken ct)
    {
        // Timeout check is handled by WizardTimeoutPreProcessor

        var result = await _mediator.Send(new CompleteWizardCommand(req.ManifestPath), ct);

        // Clear timeout tracking on successful completion
        if (result.Success)
        {
            await _wizardTimeoutService.ClearTimeoutAsync();
        }

        Response = new InstallStackResponse
        {
            Success = result.Success,
            StackVersion = result.StackVersion,
            DeployedContexts = result.DeployedContexts,
            Errors = result.Errors
        };
    }
}
