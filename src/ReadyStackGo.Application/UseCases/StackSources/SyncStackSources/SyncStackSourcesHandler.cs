using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

public class SyncStackSourcesHandler : IRequestHandler<SyncStackSourcesCommand, SyncStackSourcesResult>
{
    private readonly IProductSourceService _productSourceService;

    public SyncStackSourcesHandler(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    public async Task<SyncStackSourcesResult> Handle(SyncStackSourcesCommand request, CancellationToken cancellationToken)
    {
        var result = await _productSourceService.SyncAllAsync(cancellationToken);

        return new SyncStackSourcesResult(
            result.Success,
            result.StacksLoaded,
            result.SourcesSynced,
            result.Errors.ToList(),
            result.Warnings.ToList()
        );
    }
}
