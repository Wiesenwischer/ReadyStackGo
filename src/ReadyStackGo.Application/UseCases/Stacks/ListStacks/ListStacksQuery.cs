using MediatR;

namespace ReadyStackGo.Application.UseCases.Stacks.ListStacks;

public record ListStacksQuery : IRequest<ListStacksResult>;

public record ListStacksResult(IEnumerable<StackListItem> Stacks);

public record StackListItem(
    string Id,
    string SourceId,
    string SourceName,
    string Name,
    string? Description,
    string? RelativePath,
    List<string> Services,
    List<StackVariableItem> Variables,
    DateTime LastSyncedAt,
    string? Version
);

public record StackVariableItem(
    string Name,
    string? DefaultValue,
    bool IsRequired
);
