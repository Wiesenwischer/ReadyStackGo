using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

public record SyncStackSourcesCommand : IRequest<SyncStackSourcesResult>;

public record SyncStackSourcesResult(
    bool Success,
    int StacksLoaded,
    int SourcesSynced,
    List<string> Errors,
    List<string> Warnings
);
