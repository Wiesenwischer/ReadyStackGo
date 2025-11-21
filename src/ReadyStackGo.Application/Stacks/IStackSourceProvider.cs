using ReadyStackGo.Domain.Stacks;

namespace ReadyStackGo.Application.Stacks;

/// <summary>
/// Provider that reads stacks from a specific source type
/// </summary>
public interface IStackSourceProvider
{
    /// <summary>
    /// The source type this provider handles
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Check if this provider can handle the given source
    /// </summary>
    bool CanHandle(StackSource source);

    /// <summary>
    /// Load stacks from the source
    /// </summary>
    Task<IEnumerable<StackDefinition>> LoadStacksAsync(StackSource source, CancellationToken cancellationToken = default);
}
