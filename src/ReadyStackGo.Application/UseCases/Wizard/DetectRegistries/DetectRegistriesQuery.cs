using MediatR;

namespace ReadyStackGo.Application.UseCases.Wizard.DetectRegistries;

public record DetectRegistriesQuery : IRequest<DetectRegistriesResult>;

public record DetectRegistriesResult(IReadOnlyList<DetectedRegistryArea> Areas);

public record DetectedRegistryArea(
    string Host,
    string Namespace,
    string SuggestedPattern,
    string SuggestedName,
    bool IsLikelyPublic,
    bool IsConfigured,
    IReadOnlyList<string> Images);
