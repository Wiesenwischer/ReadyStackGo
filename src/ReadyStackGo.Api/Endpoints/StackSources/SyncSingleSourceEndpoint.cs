using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// POST /api/stack-sources/{id}/sync - Sync a specific stack source.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class SyncSingleSourceEndpoint : Endpoint<EmptyRequest, SyncSourcesResponse>
{
    private readonly IProductSourceService _productSourceService;

    public SyncSingleSourceEndpoint(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    public override void Configure()
    {
        Post("/api/stack-sources/{id}/sync");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var sourceId = Route<string>("id")!;
        var result = await _productSourceService.SyncSourceAsync(sourceId, ct);

        Response = new SyncSourcesResponse
        {
            Success = result.Success,
            StacksLoaded = result.StacksLoaded,
            SourcesSynced = result.SourcesSynced,
            Errors = result.Errors.ToList(),
            Warnings = result.Warnings.ToList()
        };

        if (!result.Success && result.Errors.Any(e => e.Contains("not found")))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
}
