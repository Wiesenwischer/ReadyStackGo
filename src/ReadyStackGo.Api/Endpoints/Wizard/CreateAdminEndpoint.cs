using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Administration.RegisterSystemAdmin;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/admin - Step 1: Create admin user
/// </summary>
public class CreateAdminEndpoint : Endpoint<CreateAdminRequest, CreateAdminResponse>
{
    private readonly IMediator _mediator;
    private readonly IWizardTimeoutService _wizardTimeoutService;

    public CreateAdminEndpoint(IMediator mediator, IWizardTimeoutService wizardTimeoutService)
    {
        _mediator = mediator;
        _wizardTimeoutService = wizardTimeoutService;
    }

    public override void Configure()
    {
        Post("/api/wizard/admin");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateAdminRequest req, CancellationToken ct)
    {
        // Check if wizard has timed out
        if (await _wizardTimeoutService.IsTimedOutAsync())
        {
            await _wizardTimeoutService.ResetTimeoutAsync();
            ThrowError("Wizard timeout expired. Please refresh and start again.");
            return;
        }

        var result = await _mediator.Send(
            new RegisterSystemAdminCommand(req.Username, req.Password),
            ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to create admin");
            return;
        }

        Response = new CreateAdminResponse
        {
            Success = true,
            Message = "Admin user created successfully"
        };
    }
}
