namespace ReadyStackGo.Application.UseCases.Wizard.GetWizardStatus;

using MediatR;

public record GetWizardStatusQuery : IRequest<WizardStatusResult>;

public record WizardStatusResult(
    string WizardState,
    bool IsCompleted,
    string DefaultDockerSocketPath);
