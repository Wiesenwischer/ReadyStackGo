using MediatR;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Application.Integrations.Prtg;

public sealed class GetPrtgJsonStatusHandler
    : IRequestHandler<GetPrtgJsonStatusQuery, PrtgJsonStatusResponse>
{
    private readonly ISnmpSnapshotProvider _snapshotProvider;
    private readonly IPrtgJsonStatusBuilder _builder;

    public GetPrtgJsonStatusHandler(ISnmpSnapshotProvider snapshotProvider, IPrtgJsonStatusBuilder builder)
    {
        _snapshotProvider = snapshotProvider;
        _builder = builder;
    }

    public Task<PrtgJsonStatusResponse> Handle(GetPrtgJsonStatusQuery request, CancellationToken cancellationToken)
    {
        var snapshot = _snapshotProvider.GetCurrentSnapshot();
        return Task.FromResult(_builder.Build(snapshot));
    }
}
