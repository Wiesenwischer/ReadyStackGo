namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Sends SNMP v2c traps to the receivers configured in SnmpSettings.
/// Implementation lives in Infrastructure/Snmp because it speaks UDP.
/// </summary>
public interface ISnmpTrapEmitter
{
    Task EmitAsync(SnmpTrap trap, CancellationToken cancellationToken = default);
}

/// <summary>
/// A trap that's about to be emitted. <see cref="TrapOid"/> identifies the
/// trap class (rsgoProductDeploymentFailedTrap etc.); <see cref="Variables"/>
/// carry the payload bindings.
/// </summary>
public record SnmpTrap(string TrapOid, IReadOnlyList<SnmpTrapVariable> Variables);

public record SnmpTrapVariable(string Oid, SnmpTrapValueType Type, string Value);

public enum SnmpTrapValueType
{
    OctetString = 0,
    Integer32 = 1,
}
