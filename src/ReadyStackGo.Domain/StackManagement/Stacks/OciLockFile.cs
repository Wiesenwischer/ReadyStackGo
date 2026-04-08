using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.Domain.StackManagement.Stacks;

/// <summary>
/// OCI Lock File for deterministic deployments.
/// Pins each service image to a specific digest (sha256) instead of a mutable tag.
/// Parsed from lock.json in an OCI Stack Bundle.
/// </summary>
public sealed class OciLockFile : ValueObject
{
    /// <summary>
    /// Lock file format version (e.g., "1").
    /// </summary>
    public string ApiVersion { get; }

    /// <summary>
    /// Stack name this lock file applies to.
    /// </summary>
    public string StackName { get; }

    /// <summary>
    /// Stack version (e.g., "1.0.0").
    /// </summary>
    public string StackVersion { get; }

    /// <summary>
    /// Pinned images with their digests.
    /// </summary>
    public IReadOnlyList<OciLockImage> Images { get; }

    private OciLockFile(string apiVersion, string stackName, string stackVersion, IReadOnlyList<OciLockImage> images)
    {
        ApiVersion = apiVersion;
        StackName = stackName;
        StackVersion = stackVersion;
        Images = images;
    }

    public static OciLockFile Create(string apiVersion, string stackName, string stackVersion, IEnumerable<OciLockImage> images)
    {
        if (string.IsNullOrWhiteSpace(stackName))
            throw new ArgumentException("Stack name is required.", nameof(stackName));
        if (string.IsNullOrWhiteSpace(stackVersion))
            throw new ArgumentException("Stack version is required.", nameof(stackVersion));

        return new OciLockFile(apiVersion ?? "1", stackName, stackVersion, images.ToList().AsReadOnly());
    }

    /// <summary>
    /// Resolves the digest for a service image.
    /// Returns null if no lock entry exists for the service.
    /// </summary>
    public string? ResolveDigest(string serviceName)
    {
        return Images.FirstOrDefault(i =>
            i.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))?.Digest;
    }

    /// <summary>
    /// Resolves the full image reference with digest (e.g., "nginx@sha256:abc123").
    /// Falls back to the original image:tag if no lock entry exists.
    /// </summary>
    public string ResolveImageReference(string serviceName, string originalImage)
    {
        var digest = ResolveDigest(serviceName);
        if (digest == null)
            return originalImage;

        // Extract image name without tag
        var imageName = originalImage.Contains(':')
            ? originalImage[..originalImage.LastIndexOf(':')]
            : originalImage;

        return $"{imageName}@{digest}";
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ApiVersion;
        yield return StackName;
        yield return StackVersion;
        foreach (var image in Images)
            yield return image;
    }
}

/// <summary>
/// A single pinned image entry in a lock file.
/// </summary>
public sealed class OciLockImage : ValueObject
{
    /// <summary>
    /// Service name this image belongs to (matches ServiceTemplate.Name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Full image reference (e.g., "nginx", "redis:7-alpine").
    /// </summary>
    public string Image { get; }

    /// <summary>
    /// Image tag (e.g., "1.25-alpine").
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// Image content digest (e.g., "sha256:abc123...").
    /// This is the immutable identifier.
    /// </summary>
    public string Digest { get; }

    /// <summary>
    /// Optional role hint (e.g., "init", "sidecar").
    /// </summary>
    public string? Role { get; }

    private OciLockImage(string name, string image, string tag, string digest, string? role)
    {
        Name = name;
        Image = image;
        Tag = tag;
        Digest = digest;
        Role = role;
    }

    public static OciLockImage Create(string name, string image, string tag, string digest, string? role = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(digest))
            throw new ArgumentException("Digest is required.", nameof(digest));

        return new OciLockImage(name, image ?? "", tag ?? "", digest, role);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Image;
        yield return Tag;
        yield return Digest;
        yield return Role ?? "";
    }
}
