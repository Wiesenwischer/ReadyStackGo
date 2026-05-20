using Lextm.SharpSnmpLib;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// Read model that maps OIDs to their current values for SNMP GET / GETNEXT / WALK.
///
/// Feature 1 (this commit) ships only a stub that returns null for every lookup so
/// the listener responds with noSuchObject. Feature 2 will replace this with an
/// implementation backed by SnmpSnapshotProvider.
/// </summary>
public interface IOidTree
{
    /// <summary>
    /// Look up the value for an exact OID. Returns null if the OID is not in the
    /// managed object space (caller emits noSuchObject).
    /// </summary>
    ISnmpData? Get(ObjectIdentifier oid);

    /// <summary>
    /// Find the lexicographically next OID that has a value. Returns null when
    /// there is no next object (caller emits endOfMibView).
    /// </summary>
    (ObjectIdentifier Oid, ISnmpData Value)? GetNext(ObjectIdentifier oid);
}
