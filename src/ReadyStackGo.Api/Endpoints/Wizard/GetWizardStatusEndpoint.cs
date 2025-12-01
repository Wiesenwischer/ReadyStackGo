using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Wizard.GetWizardStatus;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// GET /api/wizard/status - Get current wizard state
/// </summary>
public class GetWizardStatusEndpoint : EndpointWithoutRequest<WizardStatusResponse>
{
    private readonly IMediator _mediator;

    public GetWizardStatusEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/wizard/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWizardStatusQuery(), ct);

        Response = new WizardStatusResponse
        {
            WizardState = result.WizardState,
            IsCompleted = result.IsCompleted,
            DefaultDockerSocketPath = result.DefaultDockerSocketPath,
            Timeout = result.Timeout != null
                ? new WizardTimeoutDto
                {
                    IsTimedOut = result.Timeout.IsTimedOut,
                    RemainingSeconds = result.Timeout.RemainingSeconds,
                    ExpiresAt = result.Timeout.ExpiresAt,
                    TimeoutSeconds = result.Timeout.TimeoutSeconds
                }
                : null
        };
    }
}
