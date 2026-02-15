using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Application.UseCases.Wizard.CompleteWizard;

public class CompleteWizardHandler : IRequestHandler<CompleteWizardCommand, CompleteWizardResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ISystemConfigService _systemConfigService;

    public CompleteWizardHandler(
        IUserRepository userRepository,
        ISystemConfigService systemConfigService)
    {
        _userRepository = userRepository;
        _systemConfigService = systemConfigService;
    }

    public async Task<CompleteWizardResult> Handle(CompleteWizardCommand request, CancellationToken cancellationToken)
    {
        // Only require admin to complete wizard — organization setup moved to onboarding
        var hasAdmin = _userRepository.GetAll()
            .Any(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

        if (!hasAdmin)
        {
            return new CompleteWizardResult(
                false,
                null,
                new List<string>(),
                new List<string> { "System administrator must be created first" }
            );
        }

        // Persist wizard completion state to SystemConfig
        await _systemConfigService.SetWizardStateAsync(WizardState.Installed);

        // Wizard is complete — organization and further setup happen via onboarding checklist
        return new CompleteWizardResult(
            true,
            "v0.6.0",
            new List<string>(),
            new List<string>()
        );
    }
}
