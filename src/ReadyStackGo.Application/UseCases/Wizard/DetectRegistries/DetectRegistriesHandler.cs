using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.Application.UseCases.Wizard.DetectRegistries;

public class DetectRegistriesHandler : IRequestHandler<DetectRegistriesQuery, DetectRegistriesResult>
{
    private readonly IProductCache _productCache;
    private readonly IImageReferenceExtractor _extractor;
    private readonly IRegistryRepository _registryRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRegistryAccessChecker _accessChecker;
    private readonly ILogger<DetectRegistriesHandler> _logger;

    public DetectRegistriesHandler(
        IProductCache productCache,
        IImageReferenceExtractor extractor,
        IRegistryRepository registryRepository,
        IOrganizationRepository organizationRepository,
        IRegistryAccessChecker accessChecker,
        ILogger<DetectRegistriesHandler> logger)
    {
        _productCache = productCache;
        _extractor = extractor;
        _registryRepository = registryRepository;
        _organizationRepository = organizationRepository;
        _accessChecker = accessChecker;
        _logger = logger;
    }

    public async Task<DetectRegistriesResult> Handle(DetectRegistriesQuery request, CancellationToken cancellationToken)
    {
        // Collect all image references from cached stacks
        var imageReferences = _productCache.GetAllStacks()
            .SelectMany(s => s.Services)
            .Select(svc => svc.Image)
            .Where(img => !string.IsNullOrWhiteSpace(img))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyList<RegistryArea> areas;

        if (imageReferences.Count > 0)
        {
            areas = _extractor.GroupByRegistryArea(imageReferences);
        }
        else
        {
            // No stacks loaded — provide default registry set
            areas = GetDefaultRegistryAreas();
        }

        // Cross-reference with existing registries
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        var existingRegistries = organization != null
            ? _registryRepository.GetByOrganization(
                Domain.Deployment.OrganizationId.FromIdentityAccess(organization.Id)).ToList()
            : [];

        // Run runtime access checks in parallel for areas with images
        var checkTasks = areas.Select(area => CheckAreaAccessAsync(area, cancellationToken)).ToList();
        var accessResults = await Task.WhenAll(checkTasks);

        var detectedAreas = areas.Select((area, index) =>
        {
            var isConfigured = existingRegistries.Any(r =>
                area.Images.Any(img => r.MatchesImage(img)));

            // Use runtime check result; fall back to static hint for Unknown
            var isLikelyPublic = accessResults[index] switch
            {
                RegistryAccessLevel.Public => true,
                RegistryAccessLevel.AuthRequired => false,
                _ => area.IsLikelyPublic // Unknown — keep static hint as fallback
            };

            return new DetectedRegistryArea(
                Host: area.Host,
                Namespace: area.Namespace,
                SuggestedPattern: area.SuggestedPattern,
                SuggestedName: area.SuggestedName,
                IsLikelyPublic: isLikelyPublic,
                IsConfigured: isConfigured,
                Images: area.Images);
        }).ToList();

        return new DetectRegistriesResult(detectedAreas);
    }

    private async Task<RegistryAccessLevel> CheckAreaAccessAsync(RegistryArea area, CancellationToken ct)
    {
        if (area.Images.Count == 0)
            return RegistryAccessLevel.Unknown;

        // Pick a representative image to probe
        var parsed = _extractor.Parse(area.Images[0]);

        _logger.LogDebug("Checking registry access for {Host}/{Ns}/{Repo}",
            parsed.Host, parsed.Namespace, parsed.Repository);

        return await _accessChecker.CheckAccessAsync(
            parsed.Host, parsed.Namespace, parsed.Repository, ct);
    }

    private static IReadOnlyList<RegistryArea> GetDefaultRegistryAreas()
    {
        return
        [
            new RegistryArea(
                Host: "docker.io",
                Namespace: "library",
                SuggestedPattern: "library/*",
                SuggestedName: "Docker Hub (Official Images)",
                IsLikelyPublic: true,
                Images: []),
            new RegistryArea(
                Host: "ghcr.io",
                Namespace: "*",
                SuggestedPattern: "ghcr.io/**",
                SuggestedName: "GitHub Container Registry",
                IsLikelyPublic: false,
                Images: []),
            new RegistryArea(
                Host: "registry.gitlab.com",
                Namespace: "*",
                SuggestedPattern: "registry.gitlab.com/**",
                SuggestedName: "GitLab Container Registry",
                IsLikelyPublic: false,
                Images: []),
            new RegistryArea(
                Host: "quay.io",
                Namespace: "*",
                SuggestedPattern: "quay.io/**",
                SuggestedName: "Quay.io",
                IsLikelyPublic: false,
                Images: [])
        ];
    }
}
