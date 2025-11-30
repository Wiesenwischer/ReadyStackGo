namespace ReadyStackGo.Application.UseCases.Wizard.GetWizardStatus;

using MediatR;
using ReadyStackGo.Domain.Access.ValueObjects;
using ReadyStackGo.Domain.Identity.Repositories;

public class GetWizardStatusHandler : IRequestHandler<GetWizardStatusQuery, WizardStatusResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public GetWizardStatusHandler(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
    }

    public Task<WizardStatusResult> Handle(GetWizardStatusQuery request, CancellationToken cancellationToken)
    {
        var hasAdmin = _userRepository.GetAll()
            .Any(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

        var organizations = _organizationRepository.GetAll().ToList();
        var hasOrganization = organizations.Any();

        string wizardState;
        if (!hasAdmin)
        {
            wizardState = "NotStarted";
        }
        else if (!hasOrganization)
        {
            wizardState = "AdminCreated";
        }
        else
        {
            wizardState = "Installed";
        }

        var defaultDockerSocketPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        return Task.FromResult(new WizardStatusResult(
            wizardState,
            wizardState == "Installed",
            defaultDockerSocketPath));
    }
}
