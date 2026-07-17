using MediatR;
using ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

namespace ReadyStackGo.Application.UseCases.Stacks.GetProductReleaseNotes;

/// <summary>
/// Catalog-scoped release notes: resolves a product version's release notes directly by
/// its catalog product id (no deployment required). Reuses the deployment-scoped response
/// shape. <see cref="Locale"/> selects a localized changelog when available.
/// </summary>
public record GetCatalogProductReleaseNotesQuery(
    string ProductId,
    string? Locale = null) : IRequest<GetProductReleaseNotesResponse>;
