using Lextm.SharpSnmpLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// IOidTree that rebuilds itself from a SnmpSnapshotProvider at most once per
/// TTL window. Default TTL = 30 seconds (matches the value documented in
/// PLAN-snmp-agent.md / Entscheidungen).
///
/// Resolves the snapshot provider via a fresh DI scope so scoped repository
/// dependencies (EF DbContext etc.) work even though this tree is a singleton.
/// </summary>
public sealed class CachingOidTree : IOidTree
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SnmpAgentOptions _options;
    private readonly ILogger<CachingOidTree> _logger;
    private readonly object _lock = new();

    private MaterializedOidTree? _tree;
    private DateTime _builtAt = DateTime.MinValue;

    public CachingOidTree(
        IServiceScopeFactory scopeFactory,
        IOptions<SnmpAgentOptions> options,
        ILogger<CachingOidTree> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public ISnmpData? Get(ObjectIdentifier oid) => Current().Get(oid);

    public (ObjectIdentifier Oid, ISnmpData Value)? GetNext(ObjectIdentifier oid) => Current().GetNext(oid);

    private MaterializedOidTree Current()
    {
        lock (_lock)
        {
            if (_tree is null || DateTime.UtcNow - _builtAt >= Ttl)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var provider = scope.ServiceProvider.GetRequiredService<ISnmpSnapshotProvider>();
                    var snapshot = provider.GetCurrentSnapshot();
                    _tree = OidTreeBuilder.Build(snapshot, _options.RootOid);
                    _builtAt = DateTime.UtcNow;
                    _logger.LogDebug(
                        "Rebuilt SNMP OID tree with {EntryCount} entries from snapshot at {SnapshotTime:O}",
                        _tree.Count, snapshot.BuiltAt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rebuild SNMP OID tree — serving previous snapshot if available");
                    if (_tree is null)
                    {
                        // No previous snapshot — return an empty tree so the agent answers
                        // noSuchObject rather than 500-equivalent.
                        _tree = new MaterializedOidTree(new Dictionary<ObjectIdentifier, ISnmpData>());
                        _builtAt = DateTime.UtcNow;
                    }
                }
            }

            return _tree!;
        }
    }
}
