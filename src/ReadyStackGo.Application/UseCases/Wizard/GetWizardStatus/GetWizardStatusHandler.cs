namespace ReadyStackGo.Application.UseCases.Wizard.GetWizardStatus;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class GetWizardStatusHandler : IRequestHandler<GetWizardStatusQuery, WizardStatusResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ISystemConfigService _systemConfigService;
    private readonly IWizardTimeoutService _wizardTimeoutService;
    private readonly ILogger<GetWizardStatusHandler> _logger;

    public GetWizardStatusHandler(
        IUserRepository userRepository,
        ISystemConfigService systemConfigService,
        IWizardTimeoutService wizardTimeoutService,
        ILogger<GetWizardStatusHandler> logger)
    {
        _userRepository = userRepository;
        _systemConfigService = systemConfigService;
        _wizardTimeoutService = wizardTimeoutService;
        _logger = logger;
    }

    public async Task<WizardStatusResult> Handle(GetWizardStatusQuery request, CancellationToken cancellationToken)
    {
        var defaultDockerSocketPath = GetDefaultDockerSocketPath();

        // First check the persisted wizard state from SystemConfig
        var wizardState = await _systemConfigService.GetWizardStateAsync();

        // If wizard is marked as Installed, trust that state (no timeout applies)
        if (wizardState == WizardState.Installed)
        {
            return new WizardStatusResult("Installed", true, defaultDockerSocketPath);
        }

        // Once a system administrator exists, setup is done: no timeout applies, even if the
        // setup window ran out while the remaining steps (SMTP, a sign-in run of an identity
        // provider) were still open.
        var hasAdmin = _userRepository.GetAll()
            .Any(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));
        if (hasAdmin)
        {
            return new WizardStatusResult("Installed", true, defaultDockerSocketPath);
        }

        // Get timeout info - this also initializes the timeout window on first access
        var timeoutInfo = await _wizardTimeoutService.GetTimeoutInfoAsync();

        // Check if wizard has timed out
        if (timeoutInfo.IsTimedOut)
        {
            _logger.LogWarning("Wizard timeout reached. Resetting wizard state.");
            await _wizardTimeoutService.ResetTimeoutAsync();

            // Return fresh state after reset
            var freshTimeoutInfo = await _wizardTimeoutService.GetTimeoutInfoAsync();
            return new WizardStatusResult("NotStarted", false, defaultDockerSocketPath, freshTimeoutInfo);
        }

        return new WizardStatusResult(
            "NotStarted",
            false,
            defaultDockerSocketPath,
            timeoutInfo);
    }

    private static string GetDefaultDockerSocketPath()
    {
        return OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
    }
}
