using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.StackManagement.Sources;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.Application.UseCases.Onboarding.GetOnboardingStatus;

public class GetOnboardingStatusHandler : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusResult>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IStackSourceRepository _stackSourceRepository;
    private readonly IRegistryRepository _registryRepository;
    private readonly IOnboardingStateService _onboardingStateService;

    public GetOnboardingStatusHandler(
        IOrganizationRepository organizationRepository,
        IEnvironmentRepository environmentRepository,
        IStackSourceRepository stackSourceRepository,
        IRegistryRepository registryRepository,
        IOnboardingStateService onboardingStateService)
    {
        _organizationRepository = organizationRepository;
        _environmentRepository = environmentRepository;
        _stackSourceRepository = stackSourceRepository;
        _registryRepository = registryRepository;
        _onboardingStateService = onboardingStateService;
    }

    public async Task<OnboardingStatusResult> Handle(GetOnboardingStatusQuery request, CancellationToken cancellationToken)
    {
        // Check organization
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        var hasOrg = organization != null;

        // Count resources (scoped to organization if it exists)
        var envCount = 0;
        var registryCount = 0;

        if (hasOrg)
        {
            var deploymentOrgId = DeploymentOrganizationId.FromIdentityAccess(organization!.Id);
            envCount = _environmentRepository.GetByOrganization(deploymentOrgId).Count();
            registryCount = _registryRepository.GetByOrganization(deploymentOrgId).Count();
        }

        var sources = await _stackSourceRepository.GetAllAsync(cancellationToken);
        var sourceCount = sources.Count;

        var isDismissed = await _onboardingStateService.IsDismissedAsync(cancellationToken);

        return new OnboardingStatusResult(
            IsComplete: hasOrg,
            IsDismissed: isDismissed,
            Organization: new OnboardingItemStatus(hasOrg, hasOrg ? 1 : 0, organization?.Name),
            Environment: new OnboardingItemStatus(envCount > 0, envCount),
            StackSources: new OnboardingItemStatus(sourceCount > 0, sourceCount),
            Registries: new OnboardingItemStatus(registryCount > 0, registryCount));
    }
}
