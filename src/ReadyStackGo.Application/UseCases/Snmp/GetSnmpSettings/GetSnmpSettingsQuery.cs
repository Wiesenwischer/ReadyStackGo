using MediatR;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Application.UseCases.Snmp.GetSnmpSettings;

public record GetSnmpSettingsQuery() : IRequest<SnmpSettingsDto>;

public record SnmpSettingsDto(
    bool Enabled,
    int Port,
    string ListenAddress,
    string RootOid,
    string Community,
    string TrapReceivers,
    int V3UserCount);

public sealed class GetSnmpSettingsHandler : IRequestHandler<GetSnmpSettingsQuery, SnmpSettingsDto>
{
    private readonly ISnmpSettingsRepository _settings;
    private readonly ISnmpV3UserRepository _users;

    public GetSnmpSettingsHandler(ISnmpSettingsRepository settings, ISnmpV3UserRepository users)
    {
        _settings = settings;
        _users = users;
    }

    public Task<SnmpSettingsDto> Handle(GetSnmpSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = _settings.GetOrCreate();
        var count = _users.GetAll().Count;
        return Task.FromResult(new SnmpSettingsDto(
            s.Enabled, s.Port, s.ListenAddress, s.RootOid,
            string.IsNullOrEmpty(s.Community) ? string.Empty : "***",
            s.TrapReceivers,
            count));
    }
}
