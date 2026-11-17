namespace ReadyStackGo.Domain.Deployment.Edge;

/// <summary>
/// What the edge should currently be doing with traffic.
/// </summary>
public enum EdgeMode
{
    /// <summary>Transparently proxy all traffic to the upstream.</summary>
    Proxy,

    /// <summary>Serve the controlled maintenance page (health endpoints still pass through).</summary>
    Maintenance
}

/// <summary>
/// Machine-readable status the edge advertises (stable contract, finalized in Phase 5).
/// </summary>
public enum EdgeStatusState
{
    /// <summary>Product running, edge proxying.</summary>
    Running,

    /// <summary>Product is being (re)deployed/upgraded — temporarily unavailable.</summary>
    Deploying,

    /// <summary>Maintenance mode (planned, via flag) or otherwise down.</summary>
    Maintenance
}

/// <summary>
/// The desired edge state computed from RSGO's authoritative deploy state plus the
/// maintenance flag. This is a pure projection (no Docker/IO) so it is exhaustively
/// unit-testable. The edge config builder turns it into a Caddy config; the status
/// fields are surfaced verbatim in the <c>/__status</c> JSON.
/// </summary>
public sealed record EdgeDesiredState(
    EdgeMode Mode,
    EdgeStatusState StatusState,
    bool PlannedMaintenance,
    string? Reason,
    string? Until,
    string? ProductVersion)
{
    public string StatusStateToken => StatusState switch
    {
        EdgeStatusState.Running => "running",
        EdgeStatusState.Deploying => "deploying",
        EdgeStatusState.Maintenance => "maintenance",
        _ => "maintenance"
    };
}
