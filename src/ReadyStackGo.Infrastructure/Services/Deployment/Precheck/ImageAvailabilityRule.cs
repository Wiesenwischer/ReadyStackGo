using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;
using Microsoft.Extensions.Logging;

namespace ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

/// <summary>
/// Checks whether Docker images are available locally or can be pulled from the registry.
/// </summary>
public class ImageAvailabilityRule : IDeploymentPrecheckRule
{
    private readonly IDockerService _dockerService;
    private readonly IRegistryAccessChecker _registryAccessChecker;
    private readonly ILogger<ImageAvailabilityRule> _logger;

    public ImageAvailabilityRule(
        IDockerService dockerService,
        IRegistryAccessChecker registryAccessChecker,
        ILogger<ImageAvailabilityRule> logger)
    {
        _dockerService = dockerService;
        _registryAccessChecker = registryAccessChecker;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken)
    {
        var items = new List<PrecheckItem>();

        foreach (var service in context.StackDefinition.Services)
        {
            var (image, tag) = ParseImageReference(service.Image);

            try
            {
                var existsLocally = await _dockerService.ImageExistsAsync(
                    context.EnvironmentId, image, tag, cancellationToken);

                if (existsLocally)
                {
                    items.Add(new PrecheckItem(
                        "ImageAvailability",
                        PrecheckSeverity.OK,
                        $"Image available: {service.Image}",
                        "Image exists locally",
                        service.Name));
                    continue;
                }

                // Image not local — check registry
                var (host, namespacePath, repository) = ParseRegistryComponents(image);
                var accessLevel = await _registryAccessChecker.CheckAccessAsync(
                    host, namespacePath, repository, cancellationToken);

                switch (accessLevel)
                {
                    case RegistryAccessLevel.Public:
                        items.Add(new PrecheckItem(
                            "ImageAvailability",
                            PrecheckSeverity.OK,
                            $"Image pullable: {service.Image}",
                            "Image not local but available from registry (public access)",
                            service.Name));
                        break;

                    case RegistryAccessLevel.AuthRequired:
                        items.Add(new PrecheckItem(
                            "ImageAvailability",
                            PrecheckSeverity.Error,
                            $"Image requires authentication: {service.Image}",
                            $"Image not found locally and registry '{host}' requires authentication",
                            service.Name));
                        break;

                    case RegistryAccessLevel.Unknown:
                        items.Add(new PrecheckItem(
                            "ImageAvailability",
                            PrecheckSeverity.Warning,
                            $"Image availability unknown: {service.Image}",
                            $"Image not found locally and registry '{host}' could not be reached. Pull may fail.",
                            service.Name));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check image availability for {Image}", service.Image);
                items.Add(new PrecheckItem(
                    "ImageAvailability",
                    PrecheckSeverity.Warning,
                    $"Image check failed: {service.Image}",
                    $"Could not verify image availability: {ex.Message}",
                    service.Name));
            }
        }

        return items;
    }

    internal static (string Image, string Tag) ParseImageReference(string imageRef)
    {
        // Handle digest references (image@sha256:...)
        if (imageRef.Contains('@'))
        {
            var atIndex = imageRef.IndexOf('@');
            return (imageRef[..atIndex], imageRef[(atIndex + 1)..]);
        }

        // Find the last colon that is NOT part of a port in the registry host
        // e.g., "registry:5000/repo:tag" — we want tag, not 5000
        var lastColon = imageRef.LastIndexOf(':');
        if (lastColon < 0)
            return (imageRef, "latest");

        // If there's a slash after the last colon, it's a port, not a tag
        var afterColon = imageRef[(lastColon + 1)..];
        if (afterColon.Contains('/'))
            return (imageRef, "latest");

        return (imageRef[..lastColon], afterColon);
    }

    internal static (string Host, string NamespacePath, string Repository) ParseRegistryComponents(string image)
    {
        // Default Docker Hub
        const string defaultHost = "registry-1.docker.io";
        const string defaultNamespace = "library";

        var parts = image.Split('/');

        return parts.Length switch
        {
            // "nginx" → Docker Hub, library/nginx
            1 => (defaultHost, defaultNamespace, parts[0]),
            // "user/repo" → Docker Hub, user/repo
            // "ghcr.io/repo" → ghcr.io, library/repo (if first part contains dot or colon it's a host)
            2 when parts[0].Contains('.') || parts[0].Contains(':') =>
                (parts[0], defaultNamespace, parts[1]),
            2 => (defaultHost, parts[0], parts[1]),
            // "ghcr.io/user/repo" → ghcr.io, user/repo
            _ when parts[0].Contains('.') || parts[0].Contains(':') =>
                (parts[0], string.Join('/', parts[1..^1]), parts[^1]),
            _ => (defaultHost, string.Join('/', parts[..^1]), parts[^1])
        };
    }
}
