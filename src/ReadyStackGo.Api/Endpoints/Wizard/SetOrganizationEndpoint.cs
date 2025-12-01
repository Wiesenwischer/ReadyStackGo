using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Organizations.ProvisionOrganization;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/organization - Step 2: Set organization
/// </summary>
public class SetOrganizationEndpoint : Endpoint<SetOrganizationRequest, SetOrganizationResponse>
{
    private readonly IMediator _mediator;
    private readonly IWizardTimeoutService _wizardTimeoutService;

    public SetOrganizationEndpoint(IMediator mediator, IWizardTimeoutService wizardTimeoutService)
    {
        _mediator = mediator;
        _wizardTimeoutService = wizardTimeoutService;
    }

    public override void Configure()
    {
        Post("/api/wizard/organization");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SetOrganizationRequest req, CancellationToken ct)
    {
        // Check if wizard has timed out
        if (await _wizardTimeoutService.IsTimedOutAsync())
        {
            await _wizardTimeoutService.ResetTimeoutAsync();
            ThrowError("Wizard timeout expired. Please refresh and start again.");
            return;
        }

        var result = await _mediator.Send(
            new ProvisionOrganizationCommand(req.Name, req.Name),
            ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to create organization");
            return;
        }

        Response = new SetOrganizationResponse
        {
            Success = true,
            Message = "Organization set successfully"
        };
    }
}
