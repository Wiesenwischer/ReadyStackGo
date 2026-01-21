using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.GetStackSource;

public record GetStackSourceQuery(string Id) : IRequest<GetStackSourceResult?>;

public record GetStackSourceResult(
    string Id,
    string Name,
    string Type,
    bool Enabled,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    string? Path,
    string? FilePattern,
    string? GitUrl,
    string? GitBranch,
    string? GitUsername,
    bool HasGitPassword
);
