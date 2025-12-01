using MediatR;

namespace ReadyStackGo.Application.UseCases.Wizard.CompleteWizard;

public record CompleteWizardCommand(string? ManifestPath) : IRequest<CompleteWizardResult>;

public record CompleteWizardResult(
    bool Success,
    string? StackVersion,
    List<string> DeployedContexts,
    List<string> Errors
);
