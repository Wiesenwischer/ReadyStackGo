using Lextm.SharpSnmpLib;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// IOidTree backed by a sorted dictionary of (OID -> SNMP value) pairs.
/// </summary>
public sealed class MaterializedOidTree : IOidTree
{
    private readonly SortedDictionary<ObjectIdentifier, ISnmpData> _data;

    public MaterializedOidTree(IDictionary<ObjectIdentifier, ISnmpData> data)
    {
        _data = new SortedDictionary<ObjectIdentifier, ISnmpData>(data);
    }

    public int Count => _data.Count;

    public ISnmpData? Get(ObjectIdentifier oid)
        => _data.TryGetValue(oid, out var value) ? value : null;

    public (ObjectIdentifier Oid, ISnmpData Value)? GetNext(ObjectIdentifier oid)
    {
        // Sorted iteration — first entry strictly greater than oid.
        foreach (var entry in _data)
        {
            if (entry.Key.CompareTo(oid) > 0)
            {
                return (entry.Key, entry.Value);
            }
        }

        return null;
    }
}
