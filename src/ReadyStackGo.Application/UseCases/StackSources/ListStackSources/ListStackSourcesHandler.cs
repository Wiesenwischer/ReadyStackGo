using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Stacks;

namespace ReadyStackGo.Application.UseCases.StackSources.ListStackSources;

public class ListStackSourcesHandler : IRequestHandler<ListStackSourcesQuery, ListStackSourcesResult>
{
    private readonly IStackSourceService _stackSourceService;

    public ListStackSourcesHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<ListStackSourcesResult> Handle(ListStackSourcesQuery request, CancellationToken cancellationToken)
    {
        var sources = await _stackSourceService.GetSourcesAsync(cancellationToken);

        var items = sources.Select(s => new StackSourceItem(
            s.Id,
            s.Name,
            GetSourceType(s),
            s.Enabled,
            s.LastSyncedAt,
            GetSourceDetails(s)
        ));

        return new ListStackSourcesResult(items);
    }

    private static string GetSourceType(StackSource source)
    {
        return source switch
        {
            LocalDirectoryStackSource => "local-directory",
            GitRepositoryStackSource => "git-repository",
            CompositeStackSource => "composite",
            _ => "unknown"
        };
    }

    private static Dictionary<string, string> GetSourceDetails(StackSource source)
    {
        return source switch
        {
            LocalDirectoryStackSource local => new Dictionary<string, string>
            {
                ["path"] = local.Path,
                ["filePattern"] = local.FilePattern
            },
            GitRepositoryStackSource git => new Dictionary<string, string>
            {
                ["repositoryUrl"] = git.Url,
                ["branch"] = git.Branch,
                ["path"] = git.Path
            },
            CompositeStackSource composite => new Dictionary<string, string>
            {
                ["sourceCount"] = composite.Sources.Count.ToString()
            },
            _ => new Dictionary<string, string>()
        };
    }
}
