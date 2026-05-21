using MediatR;

namespace ReadyStackGo.Application.Snmp.Prtg;

public sealed class GetPrtgBundleHandler : IRequestHandler<GetPrtgBundleQuery, PrtgBundleResult>
{
    private readonly IPrtgBundleBuilder _builder;
    private readonly ISnmpRuntimeSettingsProvider _settings;

    public GetPrtgBundleHandler(IPrtgBundleBuilder builder, ISnmpRuntimeSettingsProvider settings)
    {
        _builder = builder;
        _settings = settings;
    }

    public Task<PrtgBundleResult> Handle(GetPrtgBundleQuery request, CancellationToken cancellationToken)
    {
        var current = _settings.Load();
        var result = _builder.Build(new PrtgBundleInput
        {
            RootOid = current.RootOid,
            MibBytes = request.MibBytes,
            RsgoVersion = request.RsgoVersion,
            SourceHost = request.SourceHost,
            GeneratedAtUtc = DateTime.UtcNow,
        });
        return Task.FromResult(result);
    }
}
