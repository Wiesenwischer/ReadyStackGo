using FastEndpoints;
using ReadyStackGo.Application.Stacks;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// Sync all stack sources to refresh the cache
/// </summary>
public class SyncSourcesEndpoint : EndpointWithoutRequest<SyncSourcesResponse>
{
    public IStackSourceService StackSourceService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/stack-sources/sync");
        Roles("admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await StackSourceService.SyncAllAsync(ct);

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
