using MediatR;
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

    public DetectRegistriesHandler(
        IProductCache productCache,
        IImageReferenceExtractor extractor,
        IRegistryRepository registryRepository,
        IOrganizationRepository organizationRepository)
    {
        _productCache = productCache;
        _extractor = extractor;
        _registryRepository = registryRepository;
        _organizationRepository = organizationRepository;
    }

    public Task<DetectRegistriesResult> Handle(DetectRegistriesQuery request, CancellationToken cancellationToken)
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

        var detectedAreas = areas.Select(area =>
        {
            var isConfigured = existingRegistries.Any(r =>
                area.Images.Any(img => r.MatchesImage(img)));

            return new DetectedRegistryArea(
                Host: area.Host,
                Namespace: area.Namespace,
                SuggestedPattern: area.SuggestedPattern,
                SuggestedName: area.SuggestedName,
                IsLikelyPublic: area.IsLikelyPublic,
                IsConfigured: isConfigured,
                Images: area.Images);
        }).ToList();

        return Task.FromResult(new DetectRegistriesResult(detectedAreas));
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
