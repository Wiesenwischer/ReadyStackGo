using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Wizard.CompleteWizard;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/install - Step 3: Complete wizard setup
/// </summary>
public class InstallStackEndpoint : Endpoint<InstallStackRequest, InstallStackResponse>
{
    private readonly IMediator _mediator;

    public InstallStackEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/wizard/install");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InstallStackRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CompleteWizardCommand(req.ManifestPath), ct);

        Response = new InstallStackResponse
        {
            Success = result.Success,
            StackVersion = result.StackVersion,
            DeployedContexts = result.DeployedContexts,
            Errors = result.Errors
        };
    }
}
