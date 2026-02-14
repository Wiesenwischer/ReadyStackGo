using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Parses Docker image references and groups them by registry area (host + namespace).
/// </summary>
public class ImageReferenceExtractor : IImageReferenceExtractor
{
    private static readonly HashSet<string> DockerHubHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker.io",
        "index.docker.io",
        "registry-1.docker.io",
        "registry.hub.docker.com"
    };

    public ParsedImageReference Parse(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
            return new ParsedImageReference(imageReference, "docker.io", "library", "", null);

        var original = imageReference.Trim();
        var working = original;

        // Remove digest (@sha256:...)
        var atIndex = working.IndexOf('@');
        if (atIndex > 0)
            working = working[..atIndex];

        // Extract tag - find last colon after last slash
        string? tag = null;
        var lastSlash = working.LastIndexOf('/');
        var lastColon = working.LastIndexOf(':');

        if (lastColon > lastSlash && lastColon < working.Length - 1)
        {
            tag = working[(lastColon + 1)..];
            working = working[..lastColon];
        }

        // Now working = image name without tag/digest
        // Determine host, namespace, and repository
        var parts = working.Split('/');

        if (parts.Length == 1)
        {
            // Simple image: "nginx" → docker.io/library/nginx
            return new ParsedImageReference(original, "docker.io", "library", parts[0], tag);
        }

        if (parts.Length == 2)
        {
            // Could be "user/image" (Docker Hub) or "host/image" (custom registry)
            if (LooksLikeRegistryHost(parts[0]))
            {
                // host/image → host + library + image
                var host = NormalizeHost(parts[0]);
                return new ParsedImageReference(original, host, "library", parts[1], tag);
            }

            // user/image → docker.io + user + image
            return new ParsedImageReference(original, "docker.io", parts[0], parts[1], tag);
        }

        // 3+ parts: first part is host (or Docker Hub namespace/sub-namespace)
        if (LooksLikeRegistryHost(parts[0]))
        {
            var host = NormalizeHost(parts[0]);
            var ns = parts[1];
            var repo = string.Join("/", parts.Skip(2));
            return new ParsedImageReference(original, host, ns, repo, tag);
        }

        // No registry host → Docker Hub with nested namespace
        var dockerNs = parts[0];
        var dockerRepo = string.Join("/", parts.Skip(1));
        return new ParsedImageReference(original, "docker.io", dockerNs, dockerRepo, tag);
    }

    public IReadOnlyList<RegistryArea> GroupByRegistryArea(IEnumerable<string> imageReferences)
    {
        var parsed = imageReferences
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(Parse)
            .Where(p => !string.IsNullOrEmpty(p.Repository))
            .ToList();

        var groups = parsed
            .GroupBy(p => (Host: p.Host.ToLowerInvariant(), Namespace: p.Namespace.ToLowerInvariant()))
            .OrderBy(g => g.Key.Host)
            .ThenBy(g => g.Key.Namespace);

        var areas = new List<RegistryArea>();

        foreach (var group in groups)
        {
            var host = group.First().Host;
            var ns = group.First().Namespace;
            var images = group
                .Select(p => p.OriginalReference)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(i => i)
                .ToList();

            var suggestedPattern = BuildSuggestedPattern(host, ns);
            var suggestedName = BuildSuggestedName(host, ns);
            var isLikelyPublic = IsLikelyPublic(host, ns);

            areas.Add(new RegistryArea(
                Host: host,
                Namespace: ns,
                SuggestedPattern: suggestedPattern,
                SuggestedName: suggestedName,
                IsLikelyPublic: isLikelyPublic,
                Images: images));
        }

        return areas;
    }

    private static bool LooksLikeRegistryHost(string segment)
    {
        // Contains dot → likely a hostname (ghcr.io, registry.example.com)
        // Contains colon → port number (localhost:5000)
        return segment.Contains('.') || segment.Contains(':');
    }

    private static string NormalizeHost(string host)
    {
        // Normalize Docker Hub variants to docker.io
        if (DockerHubHosts.Contains(host))
            return "docker.io";

        return host.ToLowerInvariant();
    }

    private static string BuildSuggestedPattern(string host, string ns)
    {
        if (IsDockerHub(host))
            return $"{ns}/*";

        return $"{host}/{ns}/*";
    }

    private static string BuildSuggestedName(string host, string ns)
    {
        if (IsDockerHub(host))
            return ns == "library" ? "Docker Hub (Official Images)" : $"Docker Hub – {ns}";

        return $"{host} – {ns}";
    }

    private static bool IsLikelyPublic(string host, string ns)
    {
        // Official Docker Hub images (library namespace) are almost always public
        return IsDockerHub(host) && ns.Equals("library", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDockerHub(string host)
    {
        return DockerHubHosts.Contains(host);
    }
}
