using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

/// <summary>
/// Resolves release notes for a product version: prefers a CHANGELOG.md loaded from the
/// product's own source (returned as markdown), otherwise an external release-notes URL
/// (returned as a link, never fetched server-side — SSRF protection).
/// </summary>
public class GetProductReleaseNotesHandler
    : IRequestHandler<GetProductReleaseNotesQuery, GetProductReleaseNotesResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IProductSourceService _productSourceService;

    public GetProductReleaseNotesHandler(
        IProductDeploymentRepository repository,
        IProductSourceService productSourceService)
    {
        _repository = repository;
        _productSourceService = productSourceService;
    }

    public async Task<GetProductReleaseNotesResponse> Handle(
        GetProductReleaseNotesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return GetProductReleaseNotesResponse.Failed("Version is required.");
        }

        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return GetProductReleaseNotesResponse.Failed("Invalid product deployment ID format.");
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return GetProductReleaseNotesResponse.Failed("Product deployment not found.");
        }

        var versions = await _productSourceService.GetProductVersionsAsync(
            productDeployment.ProductGroupId, cancellationToken);

        var definition = versions.FirstOrDefault(v =>
            string.Equals(v.ProductVersion, request.Version, StringComparison.OrdinalIgnoreCase));

        if (definition == null)
        {
            return GetProductReleaseNotesResponse.Failed("Version not found in catalog.");
        }

        var availableLocales = definition.AvailableChangelogLocales;

        // Prefer own CHANGELOG (safe to render) over an external URL. Resolution order:
        //   1. localized changelog matching the requested locale (exact, then language-only),
        //   2. language-neutral CHANGELOG.md,
        //   3. first available localized changelog (stable order),
        //   4. external release-notes URL.
        var (content, resolvedLocale) = ResolveChangelog(definition, request.Locale);
        if (content != null)
        {
            return new GetProductReleaseNotesResponse
            {
                Success = true,
                Mode = "markdown",
                Content = content,
                Version = definition.ProductVersion,
                Locale = resolvedLocale,
                AvailableLocales = availableLocales
            };
        }

        if (!string.IsNullOrWhiteSpace(definition.ReleaseNotesUrl))
        {
            return new GetProductReleaseNotesResponse
            {
                Success = true,
                Mode = "url",
                Url = definition.ReleaseNotesUrl,
                Version = definition.ProductVersion
            };
        }

        return GetProductReleaseNotesResponse.Failed("No release notes available for this version.");
    }

    /// <summary>
    /// Resolves the changelog markdown for a requested locale. Returns the content and the
    /// language code that was actually served (null for the language-neutral changelog).
    /// </summary>
    private static (string? Content, string? ResolvedLocale) ResolveChangelog(
        Domain.StackManagement.Stacks.ProductDefinition definition, string? requestedLocale)
    {
        var localized = definition.LocalizedChangelogs;

        if (localized.Count > 0 && !string.IsNullOrWhiteSpace(requestedLocale))
        {
            var normalized = requestedLocale.Trim().ToLowerInvariant();

            // Exact match (e.g. "de-DE"), then language-only match (e.g. "de").
            if (localized.TryGetValue(normalized, out var exact) && !string.IsNullOrWhiteSpace(exact))
                return (exact, normalized);

            var languageOnly = normalized.Split('-')[0];
            var match = localized.Keys.FirstOrDefault(k =>
                string.Equals(k.Split('-')[0], languageOnly, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(localized[match]))
                return (localized[match], match);
        }

        // Language-neutral fallback.
        if (!string.IsNullOrWhiteSpace(definition.ChangelogMarkdown))
            return (definition.ChangelogMarkdown, null);

        // First available localized changelog (stable order) as a last resort.
        var first = definition.AvailableChangelogLocales.FirstOrDefault();
        if (first != null && localized.TryGetValue(first, out var firstContent) && !string.IsNullOrWhiteSpace(firstContent))
            return (firstContent, first);

        return (null, null);
    }
}
