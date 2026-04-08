using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.CreateStackSource;

public record CreateStackSourceCommand(CreateStackSourceRequest Request) : IRequest<CreateStackSourceResult>;

public record CreateStackSourceRequest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }  // "LocalDirectory", "GitRepository", or "OciRegistry"

    // For LocalDirectory
    public string? Path { get; init; }
    public string? FilePattern { get; init; }

    // For GitRepository
    public string? GitUrl { get; init; }
    public string? Branch { get; init; }
    public string? GitUsername { get; init; }
    public string? GitPassword { get; init; }
    public bool? SslVerify { get; init; }

    // For OciRegistry
    public string? RegistryUrl { get; init; }
    public string? Repository { get; init; }
    public string? RegistryUsername { get; init; }
    public string? RegistryPassword { get; init; }
    public string? TagPattern { get; init; }
}

public record CreateStackSourceResult(
    bool Success,
    string? Message = null,
    string? SourceId = null
);
