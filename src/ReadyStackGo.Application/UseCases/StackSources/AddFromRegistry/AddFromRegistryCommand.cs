using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.AddFromRegistry;

public record AddFromRegistryCommand(string RegistrySourceId) : IRequest<AddFromRegistryResult>;

public record AddFromRegistryResult(
    bool Success,
    string? Message = null,
    string? SourceId = null);
