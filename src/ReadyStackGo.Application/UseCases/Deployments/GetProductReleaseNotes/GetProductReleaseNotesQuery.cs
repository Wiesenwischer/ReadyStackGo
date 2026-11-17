using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

/// <summary>
/// Query to fetch release notes for a specific version of a product deployment's product.
/// <see cref="Locale"/> selects a localized CHANGELOG.&lt;locale&gt;.md when available;
/// null/empty resolves to the language-neutral changelog or the first available language.
/// </summary>
public record GetProductReleaseNotesQuery(
    string ProductDeploymentId,
    string Version,
    string? Locale = null) : IRequest<GetProductReleaseNotesResponse>;

/// <summary>
/// Release notes for a product version. <see cref="Mode"/> is "markdown" (own CHANGELOG.md,
/// returned in <see cref="Content"/>) or "url" (external link in <see cref="Url"/>).
/// </summary>
public record GetProductReleaseNotesResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>"markdown" | "url" | "none".</summary>
    public string Mode { get; init; } = "none";

    /// <summary>Markdown content (only for mode "markdown").</summary>
    public string? Content { get; init; }

    /// <summary>External URL (only for mode "url"). Rendered as a link, never embedded.</summary>
    public string? Url { get; init; }

    public string? Version { get; init; }

    /// <summary>
    /// Language code of the returned markdown (only when a localized changelog was served),
    /// so the viewer can highlight the active language. Null for the language-neutral
    /// changelog or non-markdown modes.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Language codes for which a localized changelog exists. When more than one is present
    /// the viewer offers a language selector. Empty for single-language / URL / none.
    /// </summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = new List<string>();

    public static GetProductReleaseNotesResponse Failed(string message) =>
        new() { Success = false, Message = message, Mode = "none" };
}
