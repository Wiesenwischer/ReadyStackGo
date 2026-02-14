using MediatR;

namespace ReadyStackGo.Application.UseCases.Wizard.SetSources;

public record SetSourcesCommand(IReadOnlyList<string> RegistrySourceIds) : IRequest<SetSourcesResult>;

public record SetSourcesResult(bool Success, string? Message = null, int SourcesCreated = 0);
