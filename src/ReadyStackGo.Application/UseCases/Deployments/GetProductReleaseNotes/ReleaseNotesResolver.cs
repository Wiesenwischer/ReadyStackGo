using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

/// <summary>
/// Shared resolution of a product version's release notes, used by both the
/// deployment-scoped and the catalog-scoped endpoints. Prefers a CHANGELOG (safe to
/// render) over an external URL; picks a localized changelog matching the requested
/// locale, then the language-neutral CHANGELOG.md, then the first available language.
/// </summary>
public static class ReleaseNotesResolver
{
    public static GetProductReleaseNotesResponse Resolve(ProductDefinition definition, string? locale)
    {
        var (content, resolvedLocale) = ResolveChangelog(definition, locale);
        if (content != null)
        {
            return new GetProductReleaseNotesResponse
            {
                Success = true,
                Mode = "markdown",
                Content = content,
                Version = definition.ProductVersion,
                Locale = resolvedLocale,
                AvailableLocales = definition.AvailableChangelogLocales
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
    /// Resolution order: exact locale → language-only → neutral CHANGELOG.md → first available.
    /// </summary>
    private static (string? Content, string? ResolvedLocale) ResolveChangelog(
        ProductDefinition definition, string? requestedLocale)
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
