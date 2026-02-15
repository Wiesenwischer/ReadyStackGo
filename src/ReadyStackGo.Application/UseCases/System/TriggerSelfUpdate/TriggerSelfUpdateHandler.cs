using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.TriggerSelfUpdate;

/// <summary>
/// Handles the self-update command by validating the target version
/// and delegating to ISelfUpdateService.
/// </summary>
public class TriggerSelfUpdateHandler : IRequestHandler<TriggerSelfUpdateCommand, TriggerSelfUpdateResponse>
{
    private readonly ISelfUpdateService _selfUpdateService;
    private readonly IVersionCheckService _versionCheckService;
    private readonly ILogger<TriggerSelfUpdateHandler> _logger;

    public TriggerSelfUpdateHandler(
        ISelfUpdateService selfUpdateService,
        IVersionCheckService versionCheckService,
        ILogger<TriggerSelfUpdateHandler> logger)
    {
        _selfUpdateService = selfUpdateService;
        _versionCheckService = versionCheckService;
        _logger = logger;
    }

    public Task<TriggerSelfUpdateResponse> Handle(TriggerSelfUpdateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetVersion))
        {
            return Task.FromResult(new TriggerSelfUpdateResponse
            {
                Success = false,
                Message = "Target version must be specified."
            });
        }

        // Validate that the target version is actually newer
        var currentVersion = _versionCheckService.GetCurrentVersion();
        if (!IsNewerVersion(currentVersion, request.TargetVersion))
        {
            return Task.FromResult(new TriggerSelfUpdateResponse
            {
                Success = false,
                Message = $"Version {request.TargetVersion} is not newer than the current version {currentVersion}."
            });
        }

        _logger.LogInformation("Self-update requested: {Current} -> {Target}",
            currentVersion, request.TargetVersion);

        // TriggerUpdate starts the update in background and returns immediately
        var result = _selfUpdateService.TriggerUpdate(request.TargetVersion);

        return Task.FromResult(new TriggerSelfUpdateResponse
        {
            Success = result.Success,
            Message = result.Message
        });
    }

    internal static bool IsNewerVersion(string currentVersion, string targetVersion)
    {
        var current = currentVersion.TrimStart('v', 'V');
        var target = targetVersion.TrimStart('v', 'V');

        if (Version.TryParse(current, out var currentVer) &&
            Version.TryParse(target, out var targetVer))
        {
            return targetVer > currentVer;
        }

        return string.Compare(target, current, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
