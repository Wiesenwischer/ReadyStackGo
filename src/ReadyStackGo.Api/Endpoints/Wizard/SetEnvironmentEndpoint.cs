using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/environment - Step 3: Create environment (optional)
/// </summary>
public class SetEnvironmentEndpoint : Endpoint<SetEnvironmentRequest, SetEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public SetEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/wizard/environment");
        AllowAnonymous();
        PreProcessor<WizardTimeoutPreProcessor<SetEnvironmentRequest>>();
    }

    public override async Task HandleAsync(SetEnvironmentRequest req, CancellationToken ct)
    {
        // Timeout check is handled by WizardTimeoutPreProcessor

        var result = await _mediator.Send(
            new CreateEnvironmentCommand(req.Name, req.SocketPath),
            ct);

        if (!result.Success)
        {
            ThrowError(result.Message ?? "Failed to create environment");
            return;
        }

        Response = new SetEnvironmentResponse
        {
            Success = true,
            Message = "Environment created successfully",
            EnvironmentId = result.Environment?.Id
        };
    }
}
