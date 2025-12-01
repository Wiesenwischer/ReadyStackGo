using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// Sync all stack sources to refresh the cache
/// </summary>
public class SyncSourcesEndpoint : EndpointWithoutRequest<SyncSourcesResponse>
{
    private readonly IMediator _mediator;

    public SyncSourcesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/stack-sources/sync");
        Roles("admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new SyncStackSourcesCommand(), ct);

        Response = new SyncSourcesResponse
        {
            Success = result.Success,
            StacksLoaded = result.StacksLoaded,
            SourcesSynced = result.SourcesSynced,
            Errors = result.Errors,
            Warnings = result.Warnings
        };
    }
}

public class SyncSourcesResponse
{
    public bool Success { get; init; }
    public int StacksLoaded { get; init; }
    public int SourcesSynced { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
