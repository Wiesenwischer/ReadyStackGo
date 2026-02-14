using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.ListRegistrySources;

public record ListRegistrySourcesQuery : IRequest<ListRegistrySourcesResult>;

public record ListRegistrySourcesResult(IReadOnlyList<RegistrySourceItem> Sources);

public record RegistrySourceItem(
    string Id,
    string Name,
    string Description,
    string GitUrl,
    string GitBranch,
    string Category,
    IReadOnlyList<string> Tags,
    bool Featured,
    int StackCount,
    bool AlreadyAdded);
