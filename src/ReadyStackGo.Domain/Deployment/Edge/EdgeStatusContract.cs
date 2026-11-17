namespace ReadyStackGo.Domain.Deployment.Edge;

/// <summary>
/// The stable, versioned contract for the edge's machine-readable status endpoint
/// (<c>GET /__status</c>). A client/launcher relies on this shape; it is identical regardless
/// of the visual branding stage (default/bundle/container) and is the single source of truth
/// for the schema version.
///
/// Shape (schema 1):
/// <code>
/// { "schema": 1,
///   "state": "running" | "maintenance" | "deploying",
///   "reason": &lt;string|null&gt;,           // planned-maintenance reason (from the flag), else null
///   "until": &lt;iso8601|null&gt;,            // announced-until, when available, else null
///   "productVersion": &lt;string|null&gt; }
/// </code>
///
/// <list type="bullet">
/// <item><c>running</c>: product up, edge proxying.</item>
/// <item><c>maintenance</c>: planned maintenance (operator/observer flag) or otherwise down.</item>
/// <item><c>deploying</c>: a (re)deploy/upgrade is in progress (temporarily unavailable).</item>
/// </list>
/// The flag distinguishes planned maintenance (<c>maintenance</c> + reason) from a redeploy
/// (<c>deploying</c>); both are driven purely by RSGO's authoritative state, never health-guessing.
/// </summary>
public static class EdgeStatusContract
{
    /// <summary>Current status schema version.</summary>
    public const int SchemaVersion = 1;
}
