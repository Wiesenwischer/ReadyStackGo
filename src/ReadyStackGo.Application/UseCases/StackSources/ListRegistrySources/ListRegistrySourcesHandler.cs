using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.StackSources.ListRegistrySources;

public class ListRegistrySourcesHandler : IRequestHandler<ListRegistrySourcesQuery, ListRegistrySourcesResult>
{
    private readonly ISourceRegistryService _registryService;
    private readonly IProductSourceService _productSourceService;

    public ListRegistrySourcesHandler(
        ISourceRegistryService registryService,
        IProductSourceService productSourceService)
    {
        _registryService = registryService;
        _productSourceService = productSourceService;
    }

    public async Task<ListRegistrySourcesResult> Handle(
        ListRegistrySourcesQuery request,
        CancellationToken cancellationToken)
    {
        var registryEntries = _registryService.GetAll();
        var existingSources = await _productSourceService.GetSourcesAsync(cancellationToken);

        var existingGitUrls = existingSources
            .Where(s => !string.IsNullOrEmpty(s.GitUrl))
            .Select(s => s.GitUrl!.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = registryEntries.Select(e => new RegistrySourceItem(
            Id: e.Id,
            Name: e.Name,
            Description: e.Description,
            GitUrl: e.GitUrl,
            GitBranch: e.GitBranch,
            Category: e.Category,
            Tags: e.Tags,
            Featured: e.Featured,
            StackCount: e.StackCount,
            AlreadyAdded: existingGitUrls.Contains(e.GitUrl.TrimEnd('/'))
        )).ToList();

        return new ListRegistrySourcesResult(items);
    }
}
