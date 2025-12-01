namespace ReadyStackGo.Application.UseCases.Wizard.GetWizardStatus;

using MediatR;
using ReadyStackGo.Application.Services;

public record GetWizardStatusQuery : IRequest<WizardStatusResult>;

public record WizardStatusResult(
    string WizardState,
    bool IsCompleted,
    string DefaultDockerSocketPath,
    WizardTimeoutInfo? Timeout = null);
