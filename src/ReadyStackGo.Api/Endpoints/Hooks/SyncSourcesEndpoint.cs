using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.SyncStackSources;

namespace ReadyStackGo.Api.Endpoints.Hooks;

public record SyncSourcesHookResponse
{
    public bool Success { get; init; }
    public int StacksLoaded { get; init; }
    public int SourcesSynced { get; init; }
    public string? Message { get; init; }
}

[RequirePermission("Hooks", "SyncSources")]
public class SyncSourcesEndpoint : EndpointWithoutRequest<SyncSourcesHookResponse>
{
    private readonly IMediator _mediator;

    public SyncSourcesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/hooks/sync-sources");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new SyncStackSourcesCommand(), ct);

        if (!result.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }

        Response = new SyncSourcesHookResponse
        {
            Success = result.Success,
            StacksLoaded = result.StacksLoaded,
            SourcesSynced = result.SourcesSynced,
            Message = result.Success
                ? $"Synced {result.SourcesSynced} source(s), loaded {result.StacksLoaded} stack(s)."
                : $"Sync completed with errors: {string.Join("; ", result.Errors)}"
        };
    }
}
