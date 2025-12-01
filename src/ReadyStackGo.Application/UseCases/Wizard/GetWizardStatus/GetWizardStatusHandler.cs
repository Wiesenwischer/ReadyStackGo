namespace ReadyStackGo.Application.UseCases.Wizard.GetWizardStatus;

using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class GetWizardStatusHandler : IRequestHandler<GetWizardStatusQuery, WizardStatusResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISystemConfigService _systemConfigService;

    public GetWizardStatusHandler(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ISystemConfigService systemConfigService)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _systemConfigService = systemConfigService;
    }

    public async Task<WizardStatusResult> Handle(GetWizardStatusQuery request, CancellationToken cancellationToken)
    {
        // First check the persisted wizard state from SystemConfig
        var wizardState = await _systemConfigService.GetWizardStateAsync();

        // If wizard is marked as Installed, trust that state
        if (wizardState == WizardState.Installed)
        {
            var defaultPath = GetDefaultDockerSocketPath();
            return new WizardStatusResult("Installed", true, defaultPath);
        }

        // Otherwise derive state from database content
        var hasAdmin = _userRepository.GetAll()
            .Any(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

        var hasOrganization = _organizationRepository.GetAll().Any();

        string wizardStateString;
        if (!hasAdmin)
        {
            wizardStateString = "NotStarted";
        }
        else if (!hasOrganization)
        {
            wizardStateString = "AdminCreated";
        }
        else
        {
            // Organization exists - show Environment step (optional)
            // User must explicitly complete the wizard via /api/wizard/install
            wizardStateString = "OrganizationSet";
        }

        var defaultDockerSocketPath = GetDefaultDockerSocketPath();

        return new WizardStatusResult(
            wizardStateString,
            wizardStateString == "Installed",
            defaultDockerSocketPath);
    }

    private static string GetDefaultDockerSocketPath()
    {
        return OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
    }
}
