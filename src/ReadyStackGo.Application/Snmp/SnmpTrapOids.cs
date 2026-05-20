namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Stable OID suffixes (relative to <c>SnmpAgentOptions.RootOid</c>) for the
/// trap classes RSGO emits. Kept in Application so the trap emitter, the
/// notification handlers, and any documentation generators share one source.
///
/// Layout under <c>rsgoRoot</c>:
///   .6  rsgoNotifications
///       .1  rsgoProductDeploymentFailedTrap
///       .2  rsgoProductDeploymentAutoFinalizedTrap
///       .3  rsgoProductMaintenanceModeChangedTrap
///   .7  rsgoTrapVarBinds
///       .1  rsgoTrapProductId       (STRING)
///       .2  rsgoTrapProductName     (STRING)
///       .3  rsgoTrapProductVersion  (STRING)
///       .4  rsgoTrapStatus          (Integer32)
///       .5  rsgoTrapStatusText      (STRING)
///       .6  rsgoTrapMessage         (STRING)
///       .7  rsgoTrapOperationMode   (Integer32)
/// </summary>
public static class SnmpTrapOids
{
    public const string NotificationsSuffix = "6";
    public const string TrapProductDeploymentFailed = "6.1";
    public const string TrapProductDeploymentAutoFinalized = "6.2";
    public const string TrapProductMaintenanceModeChanged = "6.3";

    public const string VarBindBase = "7";
    public const string VarProductId = "7.1.0";
    public const string VarProductName = "7.2.0";
    public const string VarProductVersion = "7.3.0";
    public const string VarStatus = "7.4.0";
    public const string VarStatusText = "7.5.0";
    public const string VarMessage = "7.6.0";
    public const string VarOperationMode = "7.7.0";
}
