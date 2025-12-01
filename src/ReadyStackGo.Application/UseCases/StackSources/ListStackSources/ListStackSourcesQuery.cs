using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.ListStackSources;

public record ListStackSourcesQuery : IRequest<ListStackSourcesResult>;

public record ListStackSourcesResult(IEnumerable<StackSourceItem> Sources);

public record StackSourceItem(
    string Id,
    string Name,
    string Type,
    bool Enabled,
    DateTime? LastSyncedAt,
    Dictionary<string, string> Details
);
