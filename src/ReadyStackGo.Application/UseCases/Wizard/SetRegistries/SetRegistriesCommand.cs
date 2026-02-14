using MediatR;

namespace ReadyStackGo.Application.UseCases.Wizard.SetRegistries;

public record RegistryInput(
    string Name,
    string Host,
    string Pattern,
    bool RequiresAuth,
    string? Username,
    string? Password);

public record SetRegistriesCommand(
    IReadOnlyList<RegistryInput> Registries) : IRequest<SetRegistriesResult>;

public record SetRegistriesResult(
    bool Success,
    int RegistriesCreated,
    int RegistriesSkipped);
