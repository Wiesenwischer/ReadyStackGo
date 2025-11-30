using MediatR;
using ReadyStackGo.Domain.Access.ValueObjects;
using ReadyStackGo.Domain.Identity.Repositories;

namespace ReadyStackGo.Application.UseCases.Wizard.CompleteWizard;

public class CompleteWizardHandler : IRequestHandler<CompleteWizardCommand, CompleteWizardResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public CompleteWizardHandler(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
    }

    public Task<CompleteWizardResult> Handle(CompleteWizardCommand request, CancellationToken cancellationToken)
    {
        // Validate wizard state - must have admin and organization
        var hasAdmin = _userRepository.GetAll()
            .Any(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

        if (!hasAdmin)
        {
            return Task.FromResult(new CompleteWizardResult(
                false,
                null,
                new List<string>(),
                new List<string> { "System administrator must be created first" }
            ));
        }

        var hasOrganization = _organizationRepository.GetAll().Any();
        if (!hasOrganization)
        {
            return Task.FromResult(new CompleteWizardResult(
                false,
                null,
                new List<string>(),
                new List<string> { "Organization must be set first" }
            ));
        }

        // Wizard is complete - all prerequisites are met
        return Task.FromResult(new CompleteWizardResult(
            true,
            "v0.6.0",
            new List<string>(),
            new List<string>()
        ));
    }
}
