using Lextm.SharpSnmpLib;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// Stub OID tree that holds no managed objects. Every Get / GetNext returns null
/// so the listener answers noSuchObject / endOfMibView.
///
/// Feature 1 ships with this implementation to prove the end-to-end UDP path is
/// functional. Feature 2 replaces it with an OidTreeBuilder backed by the
/// SnmpSnapshotProvider.
/// </summary>
public sealed class EmptyOidTree : IOidTree
{
    public ISnmpData? Get(ObjectIdentifier oid) => null;

    public (ObjectIdentifier Oid, ISnmpData Value)? GetNext(ObjectIdentifier oid) => null;
}
