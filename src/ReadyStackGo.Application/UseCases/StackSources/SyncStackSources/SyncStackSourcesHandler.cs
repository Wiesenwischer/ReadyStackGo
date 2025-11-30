using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

public class SyncStackSourcesHandler : IRequestHandler<SyncStackSourcesCommand, SyncStackSourcesResult>
{
    private readonly IStackSourceService _stackSourceService;

    public SyncStackSourcesHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<SyncStackSourcesResult> Handle(SyncStackSourcesCommand request, CancellationToken cancellationToken)
    {
        var result = await _stackSourceService.SyncAllAsync(cancellationToken);

        return new SyncStackSourcesResult(
            result.Success,
            result.StacksLoaded,
            result.SourcesSynced,
            result.Errors,
            result.Warnings
        );
    }
}
