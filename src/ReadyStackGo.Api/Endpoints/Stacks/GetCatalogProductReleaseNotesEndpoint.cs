using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;
using ReadyStackGo.Application.UseCases.Stacks.GetProductReleaseNotes;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/products/{productId}/release-notes?locale=de — release notes for a catalog
/// product version (own CHANGELOG(.locale).md as markdown, or an external URL), resolved
/// directly by product id without requiring a deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Stacks", "Read")]
public class GetCatalogProductReleaseNotesEndpoint
    : Endpoint<GetCatalogProductReleaseNotesRequest, GetProductReleaseNotesResponse>
{
    private readonly IMediator _mediator;

    public GetCatalogProductReleaseNotesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/products/{ProductId}/release-notes");
        PreProcessor<RbacPreProcessor<GetCatalogProductReleaseNotesRequest>>();
    }

    public override async Task HandleAsync(GetCatalogProductReleaseNotesRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetCatalogProductReleaseNotesQuery(req.ProductId, req.Locale), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                || response.Message?.Contains("No release notes", StringComparison.OrdinalIgnoreCase) == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Failed to load release notes", statusCode);
        }

        Response = response;
    }
}

public class GetCatalogProductReleaseNotesRequest
{
    /// <summary>Route-bound catalog product id (a specific version's id).</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Query param ?locale=de|en (optional). Selects a localized changelog.</summary>
    [QueryParam]
    public string? Locale { get; set; }
}
