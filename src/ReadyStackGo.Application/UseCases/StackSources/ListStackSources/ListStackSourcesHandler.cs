using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.StackSources.ListStackSources;

public class ListStackSourcesHandler : IRequestHandler<ListStackSourcesQuery, ListStackSourcesResult>
{
    private readonly IProductSourceService _productSourceService;

    public ListStackSourcesHandler(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    public async Task<ListStackSourcesResult> Handle(ListStackSourcesQuery request, CancellationToken cancellationToken)
    {
        var sources = await _productSourceService.GetSourcesAsync(cancellationToken);

        var items = sources.Select(s => new StackSourceItem(
            s.Id.Value,
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
        return source.Type.ToString();
    }

    private static Dictionary<string, string> GetSourceDetails(StackSource source)
    {
        var details = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(source.Path))
            details["path"] = source.Path;

        if (!string.IsNullOrEmpty(source.FilePattern))
            details["filePattern"] = source.FilePattern;

        if (!string.IsNullOrEmpty(source.GitUrl))
            details["repositoryUrl"] = source.GitUrl;

        if (!string.IsNullOrEmpty(source.GitBranch))
            details["branch"] = source.GitBranch;

        if (!string.IsNullOrEmpty(source.GitUsername))
            details["username"] = source.GitUsername;

        if (!string.IsNullOrEmpty(source.GitPassword))
            details["hasPassword"] = "true";

        return details;
    }
}
