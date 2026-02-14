namespace ReadyStackGo.Application.Services;

/// <summary>
/// Parsed components of a Docker image reference.
/// </summary>
public record ParsedImageReference(
    string OriginalReference,
    string Host,
    string Namespace,
    string Repository,
    string? Tag);

/// <summary>
/// A group of images sharing the same registry host and namespace.
/// </summary>
public record RegistryArea(
    string Host,
    string Namespace,
    string SuggestedPattern,
    string SuggestedName,
    bool IsLikelyPublic,
    IReadOnlyList<string> Images);

/// <summary>
/// Parses Docker image references and groups them by registry area.
/// </summary>
public interface IImageReferenceExtractor
{
    /// <summary>
    /// Parses a single image reference into its components (host, namespace, repository, tag).
    /// </summary>
    ParsedImageReference Parse(string imageReference);

    /// <summary>
    /// Groups image references by registry area (host + namespace).
    /// </summary>
    IReadOnlyList<RegistryArea> GroupByRegistryArea(IEnumerable<string> imageReferences);
}
