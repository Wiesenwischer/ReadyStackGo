using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Provider that reads products from a specific source type.
/// A product is the primary deployment unit - it contains one or more stacks
/// plus product-level configuration (metadata, maintenance observer, etc.).
/// </summary>
public interface IProductSourceProvider
{
    /// <summary>
    /// The source type this provider handles (e.g., "local-directory", "git-repository")
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Check if this provider can handle the given source
    /// </summary>
    bool CanHandle(StackSource source);

    /// <summary>
    /// Load products from the source.
    /// Each manifest file produces one ProductDefinition containing its stacks.
    /// </summary>
    Task<IEnumerable<ProductDefinition>> LoadProductsAsync(StackSource source, CancellationToken cancellationToken = default);
}
