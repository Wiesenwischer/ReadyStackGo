using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.Application.UseCases.Wizard.SetRegistries;

public class SetRegistriesHandler : IRequestHandler<SetRegistriesCommand, SetRegistriesResult>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRegistryRepository _registryRepository;
    private readonly ILogger<SetRegistriesHandler> _logger;

    public SetRegistriesHandler(
        IOrganizationRepository organizationRepository,
        IRegistryRepository registryRepository,
        ILogger<SetRegistriesHandler> logger)
    {
        _organizationRepository = organizationRepository;
        _registryRepository = registryRepository;
        _logger = logger;
    }

    public Task<SetRegistriesResult> Handle(SetRegistriesCommand request, CancellationToken cancellationToken)
    {
        if (request.Registries.Count == 0)
            return Task.FromResult(new SetRegistriesResult(true, 0, 0));

        var organization = _organizationRepository.GetAll().FirstOrDefault();
        if (organization == null)
        {
            _logger.LogWarning("No organization found — cannot create registries");
            return Task.FromResult(new SetRegistriesResult(false, 0, request.Registries.Count));
        }

        var orgId = Domain.Deployment.OrganizationId.FromIdentityAccess(organization.Id);
        var existingRegistries = _registryRepository.GetByOrganization(orgId).ToList();

        var created = 0;
        var skipped = 0;

        foreach (var input in request.Registries)
        {
            // Skip public registries — they don't need configuration
            if (!input.RequiresAuth)
            {
                _logger.LogDebug("Skipping public registry area: {Name}", input.Name);
                skipped++;
                continue;
            }

            // Skip if missing credentials
            if (string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrWhiteSpace(input.Password))
            {
                _logger.LogWarning("Skipping registry {Name}: auth required but credentials missing", input.Name);
                skipped++;
                continue;
            }

            // Skip if a registry with matching pattern already exists
            if (HasMatchingRegistry(existingRegistries, input.Pattern))
            {
                _logger.LogDebug("Skipping registry {Name}: matching pattern already exists", input.Name);
                skipped++;
                continue;
            }

            // Create new registry
            var url = NormalizeHostToUrl(input.Host);
            var registry = Registry.Create(
                RegistryId.NewId(),
                orgId,
                input.Name,
                url,
                input.Username,
                input.Password);

            registry.SetImagePatterns([input.Pattern]);

            _registryRepository.Add(registry);
            existingRegistries.Add(registry); // Track for duplicate detection within batch
            created++;

            _logger.LogInformation("Created registry {Name} with pattern {Pattern}", input.Name, input.Pattern);
        }

        _registryRepository.SaveChanges();

        return Task.FromResult(new SetRegistriesResult(true, created, skipped));
    }

    private static bool HasMatchingRegistry(IEnumerable<Registry> registries, string pattern)
    {
        return registries.Any(r =>
            r.ImagePatterns.Any(p =>
                string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeHostToUrl(string host)
    {
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return host;

        return $"https://{host}";
    }
}
