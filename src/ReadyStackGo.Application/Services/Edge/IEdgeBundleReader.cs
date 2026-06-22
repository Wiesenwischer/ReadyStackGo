namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Reads the maintenance-page HTML for <c>maintenancePage.mode: bundle</c> from the product's
/// manifest repository at deploy time. The result is persisted on the edge config so the
/// reconciler never needs manifest/file access. Implemented in infrastructure.
/// </summary>
public interface IEdgeBundleReader
{
    /// <summary>
    /// Resolves the bundle directory relative to the manifest file and returns its
    /// <c>index.html</c> content (or the file content when <paramref name="bundlePath"/> points
    /// directly at an HTML file). Returns <c>null</c> when nothing can be read.
    /// </summary>
    Task<string?> ReadBundleHtmlAsync(string? manifestFilePath, string? bundlePath, CancellationToken cancellationToken = default);
}
