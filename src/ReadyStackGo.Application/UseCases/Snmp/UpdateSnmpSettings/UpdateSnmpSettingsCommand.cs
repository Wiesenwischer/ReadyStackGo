using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Application.UseCases.Snmp.UpdateSnmpSettings;

public record UpdateSnmpSettingsCommand(
    bool Enabled,
    int Port,
    string ListenAddress,
    string RootOid,
    string Community,
    string TrapReceivers) : IRequest<UpdateSnmpSettingsResult>;

public record UpdateSnmpSettingsResult(bool Success, string? ErrorMessage);

public sealed class UpdateSnmpSettingsHandler : IRequestHandler<UpdateSnmpSettingsCommand, UpdateSnmpSettingsResult>
{
    private readonly ISnmpSettingsRepository _settings;
    private readonly ILogger<UpdateSnmpSettingsHandler> _logger;

    public UpdateSnmpSettingsHandler(
        ISnmpSettingsRepository settings,
        ILogger<UpdateSnmpSettingsHandler> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task<UpdateSnmpSettingsResult> Handle(UpdateSnmpSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var s = _settings.GetOrCreate();
            var changed = s.Update(
                request.Enabled,
                request.Port,
                request.ListenAddress,
                request.RootOid,
                request.Community,
                request.TrapReceivers);

            if (changed)
            {
                _settings.Update(s);
                _settings.SaveChanges();
                _logger.LogInformation(
                    "SNMP settings updated: Enabled={Enabled}, Port={Port}, ListenAddress={Address}, RootOid={Oid}",
                    s.Enabled, s.Port, s.ListenAddress, s.RootOid);
            }

            return Task.FromResult(new UpdateSnmpSettingsResult(true, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SNMP settings");
            return Task.FromResult(new UpdateSnmpSettingsResult(false, ex.Message));
        }
    }
}
