using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.Edge;

namespace ReadyStackGo.Infrastructure.Services.Edge;

/// <summary>
/// Reads a maintenance-page bundle (an <c>index.html</c>, or a direct <c>.html</c> file)
/// from the product's manifest directory.
/// </summary>
public class EdgeBundleReader : IEdgeBundleReader
{
    private readonly ILogger<EdgeBundleReader> _logger;

    public EdgeBundleReader(ILogger<EdgeBundleReader> logger) => _logger = logger;

    public async Task<string?> ReadBundleHtmlAsync(string? manifestFilePath, string? bundlePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestFilePath) || string.IsNullOrWhiteSpace(bundlePath))
            return null;

        try
        {
            var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestFilePath));
            if (string.IsNullOrEmpty(manifestDir))
                return null;

            var resolved = Path.GetFullPath(Path.Combine(manifestDir, bundlePath));
            var indexPath = Directory.Exists(resolved) ? Path.Combine(resolved, "index.html") : resolved;

            if (!File.Exists(indexPath))
            {
                _logger.LogWarning("Edge maintenance bundle not found at {Path}", indexPath);
                return null;
            }

            return await File.ReadAllTextAsync(indexPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read edge maintenance bundle from {BundlePath} (manifest {Manifest})",
                bundlePath, manifestFilePath);
            return null;
        }
    }
}
