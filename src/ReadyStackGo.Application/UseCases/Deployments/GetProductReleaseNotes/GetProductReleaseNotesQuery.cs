using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

/// <summary>
/// Query to fetch release notes for a specific version of a product deployment's product.
/// </summary>
public record GetProductReleaseNotesQuery(
    string ProductDeploymentId,
    string Version) : IRequest<GetProductReleaseNotesResponse>;

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

    public static GetProductReleaseNotesResponse Failed(string message) =>
        new() { Success = false, Message = message, Mode = "none" };
}
