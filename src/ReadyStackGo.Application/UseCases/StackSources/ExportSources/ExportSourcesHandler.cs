using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.StackSources.ExportSources;

public class ExportSourcesHandler : IRequestHandler<ExportSourcesQuery, ExportSourcesResult>
{
    private readonly IProductSourceService _productSourceService;

    public ExportSourcesHandler(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    public async Task<ExportSourcesResult> Handle(
        ExportSourcesQuery request,
        CancellationToken cancellationToken)
    {
        var sources = await _productSourceService.GetSourcesAsync(cancellationToken);

        var exported = sources.Select(s => new ExportedSource(
            Name: s.Name,
            Type: s.Type == StackSourceType.GitRepository ? "git-repository" : "local-directory",
            Enabled: s.Enabled,
            Path: s.Path,
            FilePattern: s.FilePattern,
            GitUrl: s.GitUrl,
            GitBranch: s.GitBranch,
            GitSslVerify: s.Type == StackSourceType.GitRepository ? s.GitSslVerify : null
        )).ToList();

        var data = new ExportData(
            Version: "1.0",
            ExportedAt: DateTime.UtcNow,
            Sources: exported);

        return new ExportSourcesResult(data);
    }
}
