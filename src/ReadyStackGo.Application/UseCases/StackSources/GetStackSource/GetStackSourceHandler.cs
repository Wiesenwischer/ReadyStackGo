using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.StackSources.GetStackSource;

public class GetStackSourceHandler : IRequestHandler<GetStackSourceQuery, GetStackSourceResult?>
{
    private readonly IProductSourceService _productSourceService;

    public GetStackSourceHandler(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    public async Task<GetStackSourceResult?> Handle(GetStackSourceQuery request, CancellationToken cancellationToken)
    {
        var sources = await _productSourceService.GetSourcesAsync(cancellationToken);
        var source = sources.FirstOrDefault(s => s.Id.Value == request.Id);

        if (source == null)
        {
            return null;
        }

        return new GetStackSourceResult(
            source.Id.Value,
            source.Name,
            source.Type.ToString(),
            source.Enabled,
            source.LastSyncedAt,
            source.CreatedAt,
            source.Path,
            source.FilePattern,
            source.GitUrl,
            source.GitBranch,
            source.GitUsername,
            !string.IsNullOrEmpty(source.GitPassword)
        );
    }
}
